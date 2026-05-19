using CrystalDecisions.CrystalReports.Engine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SSAI.GeneradorPDF.facturas
{
    class Program
    {
        static async Task Main(string[] args)
        {
            await ejecutarExportacion();
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
                        new DateTime(2026, 5, 16),
                        DateTime.Now.Date
                    );

                foreach (var factura in facturas)
                {
                    try
                    {
                        //DSAP_610301 
                        //OPT_321160
                        //DSAP_610302
                        //if (factura.contrato!= "DSAP_610301")
                        //{
                        //    continue;
                        //}

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

                        string rutaFirma = Path.Combine(
                            carpeta,
                            $"firma_{numeroFactura}.png"
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

                        pdfOk = Logic.exportacion.EsperarArchivoLibre(rutaPdfGenerado);

                        await Logic.exportacion.GuardarFacturaPdfGenerado(
                            factura.InvoiceId,
                            factura.docEntry,
                            factura.contrato,
                            factura.n_factura,
                            pdfOk,
                            factura.usuario,
                            pdfOk ? rutaPdfGenerado : null,
                            pdfOk ? null : error
                        );
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
                    }
                }
            }
            catch
            {
            }
        }





    }
}
