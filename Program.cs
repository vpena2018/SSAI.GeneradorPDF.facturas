using CrystalDecisions.CrystalReports.Engine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SSAI.GeneradorPDF.facturas
{
    class Program
    {

        static Mutex mutex = null;

        static async Task Main(string[] args)
        {
            bool createdNew;

            mutex = new Mutex(
                true,
                "SSAI_GENERADOR_FACTURAS_MUTEX",
                out createdNew
            );

            // YA HAY OTRA INSTANCIA
            if (!createdNew)
            {
                return;
            }

            try
            {

                var facturas =
                    await Logic.exportacion
                        .ObtenerFacturasParaGenerarPDF(
                            new DateTime(2026, 5, 19),
                            DateTime.Now.Date
                        );

                if (facturas.Count <= 0)
                {
                    return;
                }

                // SSAI
                await ejecutarExportacionSSAI(facturas);

                // PEQUEÑA PAUSA
                Thread.Sleep(5000);

                // COPIAS
                ejecutarExportacionCopias(facturas);

                // REINTENTO FALTANTES
                VerificarYRegenerarFacturasFaltantes(
                    facturas
                );
            }
            finally
            {
                mutex.Dispose();
            }
        }


        static async Task Main2(string[] args)
        {
            //await ejecutarExportacion();

            //var facturas =
            //await Logic.exportacion.ObtenerFacturasParaGenerarPDF(
            //    new DateTime(2026, 5, 20),
            //    DateTime.Now.Date
            //);

            var facturas =
            await Logic.exportacion.ObtenerFacturasParaGenerarPDF(
                new DateTime(2026, 5, 19),
                new DateTime(2026, 5, 19)
            );


            if (facturas.Count<=0)
            {
                return;
            }

            // SSAI
            await ejecutarExportacionSSAI(facturas);

            //LIBERAR TODO ENTRE PROCESOS
            Thread.Sleep(10000);

                        GC.Collect(
                GC.MaxGeneration,
                GCCollectionMode.Forced,
                true,
                true
            );

            GC.WaitForPendingFinalizers();

            GC.Collect(
                GC.MaxGeneration,
                GCCollectionMode.Forced,
                true,
                true
            );

            Thread.Sleep(15000);

            // COPIAS
            ejecutarExportacionCopias(facturas);

            Thread.Sleep(20000);

            //COPIAS FALTANTES
            //VerificarYRegenerarFacturasFaltantes(facturas);



        }

        private static async Task ejecutarExportacionSSAI(List<Logic.exportacion.facturaEnvioRow> facturas)
        {
            try
            {
                var usuarios = new models.Hertz_ProjectsEntities()
                    .ssai_users
                    .ToList();

                foreach (var factura in facturas)
                {
                    try
                    {
                        var rowusuario = usuarios
                            .FirstOrDefault(x =>
                                x.user_SSAI == factura.usuario);

                        byte[] firma = null;

                        if (rowusuario != null)
                        {
                            firma = rowusuario.firma;
                        }

                        var numeroFactura = factura.n_factura
                            .Replace("-", "")
                            .Trim();

                        string carpeta = Path.Combine(
                            AppDomain.CurrentDomain.BaseDirectory,
                            "firmas"
                        );

                        if (!Directory.Exists(carpeta))
                            Directory.CreateDirectory(carpeta);

                        //string rutaFirma = Path.Combine(
                        //    carpeta,
                        //    $"firma_{numeroFactura}.png"
                        //);

                        string rutaFirma = Path.Combine(
                            carpeta,
                            $"firma_{numeroFactura}_{Guid.NewGuid()}.png"
                        );

                        string rutaPdfGenerado = "";
                        string error = "Error generando PDF";

                        bool pdfOk =
                            Logic.exportacion.generatePdfFactura(
                                factura,
                                factura.docEntry,
                                numeroFactura,
                                firma,
                                rutaFirma,
                                out rutaPdfGenerado,
                                out error
                            );

                        if (pdfOk)
                        {
                            pdfOk =
                                Logic.exportacion.EsperarArchivoLibre(
                                    rutaPdfGenerado
                                );
                        }

                        await Logic.exportacion.GuardarFacturaPdfGenerado(
                            factura.InvoiceId,
                            factura.docEntry,
                            factura.contrato,
                            factura.n_factura,
                            pdfOk,
                            factura.usuario,
                            pdfOk
                                ? rutaPdfGenerado
                                : null,
                            pdfOk ? null : error
                        );

                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                        GC.Collect();

                        Thread.Sleep(2000);
                    }
                    catch (Exception ex)
                    {
                        File.AppendAllText(
                            Path.Combine(
                                AppDomain.CurrentDomain.BaseDirectory,
                                "errores_ssai.txt"
                            ),
                            $"{DateTime.Now} - {ex}\r\n\r\n"
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                File.AppendAllText(
                    Path.Combine(
                        AppDomain.CurrentDomain.BaseDirectory,
                        "errores_ssai_general.txt"
                    ),
                    $"{DateTime.Now} - {ex}\r\n\r\n"
                );
            }
        }

        private static void ejecutarExportacionCopias(List<Logic.exportacion.facturaEnvioRow> facturas)
        {
            //string carpetaTemp =
            //    @"\\10.10.1.31\scaneos\SAP\facturas_pdf_SAP\temp";

            string carpetaTemp = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "temp_pdf"
            );

            try
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                Thread.Sleep(1000);

                if (Directory.Exists(carpetaTemp))
                {
                    Directory.Delete(carpetaTemp, true);
                }
            }
            catch
            {
            }


            try
            {

                // CREAR TEMP
                if (!Directory.Exists(carpetaTemp))
                {
                    Directory.CreateDirectory(carpetaTemp);
                }

                var usuarios = new models.Hertz_ProjectsEntities()
                    .ssai_users
                    .ToList();

                foreach (var factura in facturas)
                {
                    try
                    {

                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                        GC.Collect();

                        Thread.Sleep(1000);


                        var rowusuario = usuarios
                            .FirstOrDefault(x =>
                                x.user_SSAI == factura.usuario);

                        byte[] firma = null;

                        if (rowusuario != null)
                        {
                            firma = rowusuario.firma;
                        }

                        var numeroFactura = factura.n_factura
                            .Replace("-", "")
                            .Trim();

                        string carpetaCopia = Path.Combine(
                            AppDomain.CurrentDomain.BaseDirectory,
                            "firmas_copias"
                        );

                        if (!Directory.Exists(carpetaCopia))
                            Directory.CreateDirectory(carpetaCopia);

                        //string rutaFirma = Path.Combine(
                        //    carpetaCopia,
                        //    $"firma_{numeroFactura}.png"
                        //);

                        string rutaFirma = Path.Combine(
                            carpetaCopia,
                            $"firma_{numeroFactura}_{Guid.NewGuid()}.png"
                        );

                        List<string> rutasPdfGenerados =
                            new List<string>();

                        string error = "Error generando PDF";

                        bool pdfCopiasOk =
                            Logic.exportacion.generatePdfFacturaCopias(
                                factura,
                                factura.docEntry,
                                numeroFactura,
                                firma,
                                rutaFirma,
                                out rutasPdfGenerados,
                                out error
                            );

                        if (pdfCopiasOk)
                        {
                            foreach (var rutaPdf in rutasPdfGenerados)
                            {
                                bool archivoLibre =
                                    Logic.exportacion.EsperarArchivoLibre(
                                        rutaPdf
                                    );

                                if (!archivoLibre)
                                {
                                    pdfCopiasOk = false;

                                    error =
                                        $"Archivo bloqueado: {rutaPdf}";

                                    break;
                                }
                            }

                            // MERGE
                            if (pdfCopiasOk)
                            {
                                string rutaPdfFinal = Path.Combine(
                                    @"\\10.10.1.31\scaneos\SAP\facturas_pdf_SAP",
                                    $"Factura_{factura.contrato}.pdf"
                                );

                                Logic.exportacion.UnirPdfs(
                                    rutasPdfGenerados,
                                    rutaPdfFinal
                                );

                                pdfCopiasOk =
                                    Logic.exportacion.EsperarArchivoLibre(
                                        rutaPdfFinal
                                    );
                            }
                        }

                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                        GC.Collect();

                        //Thread.Sleep(2000);
                    }
                    catch (Exception ex)
                    {
                        File.AppendAllText(
                            Path.Combine(
                                AppDomain.CurrentDomain.BaseDirectory,
                                "errores_copias.txt"
                            ),
                            $"{DateTime.Now} - {ex}\r\n\r\n"
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                File.AppendAllText(
                    Path.Combine(
                        AppDomain.CurrentDomain.BaseDirectory,
                        "errores_copias_general.txt"
                    ),
                    $"{DateTime.Now} - {ex}\r\n\r\n"
                );
            }
            finally
            {

            }
        }

        private static void VerificarYRegenerarFacturasFaltantes(
    List<Logic.exportacion.facturaEnvioRow> facturas)
        {
            try
            {
                string carpetaFinal =
                    @"\\10.10.1.31\scaneos\SAP\facturas_pdf_SAP";

                var usuarios = new models.Hertz_ProjectsEntities()
                    .ssai_users
                    .ToList();

                // BUSCAR FALTANTES
                var facturasFaltantes = facturas
                    .Where(f =>
                    {
                        string rutaPdfFinal = Path.Combine(
                            carpetaFinal,
                            $"Factura_{f.contrato}.pdf"
                        );

                        return !File.Exists(rutaPdfFinal);
                    })
                    .ToList();

                // SI NO HAY FALTANTES
                if (facturasFaltantes.Count <= 0)
                {
                    return;
                }

                // REGENERAR
                foreach (var factura in facturasFaltantes)
                {
                    try
                    {
                        var rowusuario = usuarios
                            .FirstOrDefault(x =>
                                x.user_SSAI == factura.usuario);

                        byte[] firma = null;

                        if (rowusuario != null)
                        {
                            firma = rowusuario.firma;
                        }

                        var numeroFactura = factura.n_factura
                            .Replace("-", "")
                            .Trim();

                        string carpetaCopia = Path.Combine(
                            AppDomain.CurrentDomain.BaseDirectory,
                            "firmas_copias"
                        );

                        if (!Directory.Exists(carpetaCopia))
                        {
                            Directory.CreateDirectory(carpetaCopia);
                        }

                        //string rutaFirma = Path.Combine(
                        //    carpetaCopia,
                        //    $"firma_retry_{numeroFactura}.png"
                        //);

                        string rutaFirma = Path.Combine(
                            carpetaCopia,
                            $"firma_retry_{numeroFactura}_{Guid.NewGuid()}.png"
                        );

                        List<string> rutasPdfGenerados =
                            new List<string>();

                        string error = string.Empty;

                        bool pdfCopiasOk =
                            Logic.exportacion.generatePdfFacturaCopias(
                                factura,
                                factura.docEntry,
                                numeroFactura,
                                firma,
                                rutaFirma,
                                out rutasPdfGenerados,
                                out error
                            );

                        if (!pdfCopiasOk)
                        {
                            File.AppendAllText(
                                Path.Combine(
                                    AppDomain.CurrentDomain.BaseDirectory,
                                    "errores_retry.txt"
                                ),
                                $"{DateTime.Now} - ERROR GENERANDO {factura.contrato} - {error}\r\n"
                            );

                            continue;
                        }

                        // VALIDAR TEMPORALES
                        bool todosExisten = true;

                        foreach (var rutaPdf in rutasPdfGenerados)
                        {
                            int intentos = 0;

                            while (!File.Exists(rutaPdf) &&
                                   intentos < 10)
                            {
                                Thread.Sleep(300);
                                intentos++;
                            }

                            if (!File.Exists(rutaPdf))
                            {
                                todosExisten = false;

                                File.AppendAllText(
                                    Path.Combine(
                                        AppDomain.CurrentDomain.BaseDirectory,
                                        "errores_retry.txt"
                                    ),
                                    $"{DateTime.Now} - TEMP NO EXISTE {rutaPdf}\r\n"
                                );

                                break;
                            }
                        }

                        if (!todosExisten)
                        {
                            continue;
                        }

                        // MERGE FINAL
                        string rutaPdfFinal = Path.Combine(
                            carpetaFinal,
                            $"Factura_{factura.contrato}.pdf"
                        );

                        // SI EXISTE BORRAR
                        try
                        {
                            if (File.Exists(rutaPdfFinal))
                            {
                                File.Delete(rutaPdfFinal);
                            }
                        }
                        catch
                        {
                        }

                        Logic.exportacion.UnirPdfs(
                            rutasPdfGenerados,
                            rutaPdfFinal
                        );

                        // VALIDAR FINAL
                        int intentosFinal = 0;

                        while (!File.Exists(rutaPdfFinal) &&
                               intentosFinal < 10)
                        {
                            Thread.Sleep(500);
                            intentosFinal++;
                        }

                        if (!File.Exists(rutaPdfFinal))
                        {
                            File.AppendAllText(
                                Path.Combine(
                                    AppDomain.CurrentDomain.BaseDirectory,
                                    "errores_retry.txt"
                                ),
                                $"{DateTime.Now} - FINAL NO EXISTE {factura.contrato}\r\n"
                            );
                        }
                        else
                        {
                            File.AppendAllText(
                                Path.Combine(
                                    AppDomain.CurrentDomain.BaseDirectory,
                                    "errores_retry.txt"
                                ),
                                $"{DateTime.Now} - RECUPERADA {factura.contrato}\r\n"
                            );
                        }

                        Thread.Sleep(1000);
                    }
                    catch (Exception ex)
                    {
                        File.AppendAllText(
                            Path.Combine(
                                AppDomain.CurrentDomain.BaseDirectory,
                                "errores_retry.txt"
                            ),
                            $"{DateTime.Now} - {factura.contrato} - {ex}\r\n\r\n"
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                File.AppendAllText(
                    Path.Combine(
                        AppDomain.CurrentDomain.BaseDirectory,
                        "errores_retry_general.txt"
                    ),
                    $"{DateTime.Now} - {ex}\r\n\r\n"
                );
            }
        }

        private static async Task ejecutarExportacion()
        {
            try
            {
                var usuarios = new models.Hertz_ProjectsEntities()
                    .ssai_users
                    .ToList();

                var facturas =
                    await Logic.exportacion.ObtenerFacturasParaGenerarPDF(
                        new DateTime(2026, 5, 19),
                        DateTime.Now.Date
                    );

                // TEMP GLOBAL
                string carpetaTemp =
                    @"\\10.10.1.31\scaneos\SAP\facturas_pdf_SAP\temp";



                // LIMPIAR TEMP AL INICIO
                try
                {
                    if (Directory.Exists(carpetaTemp))
                    {
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                        GC.Collect();

                        Thread.Sleep(3000);

                        Directory.Delete(carpetaTemp, true);
                    }
                }
                catch
                {
                }

                // CREAR TEMP
                if (!Directory.Exists(carpetaTemp))
                {
                    Directory.CreateDirectory(carpetaTemp);
                }

                foreach (var factura in facturas)
                {
                    try
                    {
                        var rowusuario = usuarios
                            .FirstOrDefault(x =>
                                x.user_SSAI == factura.usuario);

                        byte[] firma = null;

                        if (rowusuario != null)
                        {
                            firma = rowusuario.firma;
                        }

                        var numeroFactura = factura.n_factura
                            .Replace("-", "")
                            .Trim();

                        // FIRMAS
                        string carpeta = Path.Combine(
                            AppDomain.CurrentDomain.BaseDirectory,
                            "firmas"
                        );

                        string carpetaCopia = Path.Combine(
                            AppDomain.CurrentDomain.BaseDirectory,
                            "firmas_copias"
                        );

                        if (!Directory.Exists(carpeta))
                            Directory.CreateDirectory(carpeta);

                        if (!Directory.Exists(carpetaCopia))
                            Directory.CreateDirectory(carpetaCopia);

                        string rutaFirmaTemporales = Path.Combine(
                            carpeta,
                            $"firma_{numeroFactura}.png"
                        );

                        string rutaFirmacopias = Path.Combine(
                            carpetaCopia,
                            $"firma_{numeroFactura}.png"
                        );

                        string rutaPdfGenerado = "";
                        List<string> rutasPdfGenerados =
                            new List<string>();

                        string error = "Error generando PDF";

                        bool pdfOk = false;

                        // PDF SSAI
                        pdfOk =
                            Logic.exportacion.generatePdfFactura(
                                factura,
                                factura.docEntry,
                                numeroFactura,
                                firma,
                                rutaFirmaTemporales,
                                out rutaPdfGenerado,
                                out error
                            );

                        if (pdfOk)
                        {
                            pdfOk =
                                Logic.exportacion.EsperarArchivoLibre(
                                    rutaPdfGenerado
                                );
                        }

                        // COPIAS
                        bool pdfCopiasOk =
                            Logic.exportacion.generatePdfFacturaCopias(
                                factura,
                                factura.docEntry,
                                numeroFactura,
                                firma,
                                rutaFirmacopias,
                                out rutasPdfGenerados,
                                out error
                            );

                        string rutaPdfFinal = "";

                        if (pdfCopiasOk)
                        {
                            foreach (var rutaPdf in rutasPdfGenerados)
                            {
                                bool archivoLibre =
                                    Logic.exportacion.EsperarArchivoLibre(
                                        rutaPdf
                                    );

                                if (!archivoLibre)
                                {
                                    pdfCopiasOk = false;

                                    error =
                                        $"Archivo bloqueado: {rutaPdf}";

                                    break;
                                }
                            }

                            // MERGE
                            if (pdfCopiasOk)
                            {
                                rutaPdfFinal = Path.Combine(
                                    @"\\10.10.1.31\scaneos\SAP\facturas_pdf_SAP",
                                    $"Factura_{factura.contrato}.pdf"
                                );

                                Logic.exportacion.UnirPdfs(
                                    rutasPdfGenerados,
                                    rutaPdfFinal
                                );

                                // VALIDAR
                                pdfCopiasOk =
                                    Logic.exportacion.EsperarArchivoLibre(
                                        rutaPdfFinal
                                    );

                                // EXISTE
                                if (!File.Exists(rutaPdfFinal))
                                {
                                    pdfCopiasOk = false;

                                    error =
                                        "PDF final no generado";
                                }
                            }
                        }

                        // RESULTADO FINAL
                        pdfOk = pdfOk && pdfCopiasOk;

                        // GUARDAR
                        await Logic.exportacion.GuardarFacturaPdfGenerado(
                            factura.InvoiceId,
                            factura.docEntry,
                            factura.contrato,
                            factura.n_factura,
                            pdfOk,
                            factura.usuario,
                            pdfCopiasOk
                                ? rutaPdfFinal
                                : null,
                            pdfOk ? null : error
                        );

                        // LIMPIEZA
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                        GC.Collect();

                        Thread.Sleep(2000);
                    }
                    catch (Exception ex)
                    {
                        await Logic.exportacion.GuardarFacturaPdfGenerado(
                            factura.InvoiceId,
                            factura.docEntry,
                            factura.contrato,
                            factura.n_factura,
                            false,
                            factura.usuario,
                            null,
                            ex.ToString()
                        );

                        File.AppendAllText(
                            Path.Combine(
                                AppDomain.CurrentDomain.BaseDirectory,
                                "errores_pdf.txt"
                            ),
                            $"{DateTime.Now} - {ex}\r\n\r\n"
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                File.AppendAllText(
                    Path.Combine(
                        AppDomain.CurrentDomain.BaseDirectory,
                        "errores_pdf_general.txt"
                    ),
                    $"{DateTime.Now} - {ex}\r\n\r\n"
                );
            }
            finally
            {
                try
                {
                    string carpetaTemp =
                        @"\\10.10.1.31\scaneos\SAP\facturas_pdf_SAP\temp";

                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();

                    Thread.Sleep(3000);

                    if (Directory.Exists(carpetaTemp))
                    {
                        Directory.Delete(
                            carpetaTemp,
                            true
                        );
                    }
                }
                catch
                {
                }
            }
        }





    }
}
