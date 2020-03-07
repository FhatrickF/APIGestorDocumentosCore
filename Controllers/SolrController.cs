using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using APIGestorDocumentos.Models;
using APIGestorDocumentosCore.Exceptions;
using APIGestorDocumentosCore.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.IO;

namespace APIGestorDocumentosCore.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class SolrController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public SolrController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        /// <summary>
        /// Busca documento con la palabra clave y paginación según necesiten.
        /// </summary>
        /// <param name="texto"></param>
        /// <param name="pagina"></param>
        /// <returns>devuelve un Json con la información solicitada.</returns>
        /// <response code="401">No Autorizado. No se ha iniciado sesión.</response>               
        /// <response code="400">BadRequest. El protocolo de petición no es el correcto.</response>
        [HttpPost]
        [Route("Buscar")]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<Response> Buscar(string texto, int pagina)
        {
            Response resp = new Response();
            try
            {
                String urlAddress = "";
                string urlSolr = _configuration["webSolr"].Trim();
                if (texto == "")
                    urlAddress = urlSolr + "/solr/test-1/select?hl.fl=texto&hl=on&q=*%3A*&rows=5&start=0";
                else
                    urlAddress = urlSolr + "/solr/test-1/select?fl=id%2CIdDocumento%2CCategoria%2CNorma%2CNumero%2C%20Organismo&hl.fl=Texto&hl.simple.post=%3C%2Flabel%3E&hl.simple.pre=%3Clabel%20style%3D%22background-color%3A%20yellow%22%3E&hl=on&q=Texto%3A\"" + texto + "\"&rows=5&start=" + pagina + "&wt=json";

                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(urlAddress);
                HttpWebResponse response;
                response = (HttpWebResponse)request.GetResponse();

                string responseStr = "";
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    Stream responseStream = response.GetResponseStream();
                    responseStr = new StreamReader(responseStream).ReadToEnd();
                }

                var expConverter = new ExpandoObjectConverter();
                dynamic obj = JsonConvert.DeserializeObject<ExpandoObject>(responseStr, expConverter);

                resp.Code = HttpStatusCode.OK.ToString();
                resp.Message = string.Empty;
                resp.Data = JsonConvert.SerializeObject(obj.response);
                return resp;
                //var lista = JsonConvert.DeserializeObject<SolrXml>(responseStr);

                //return Task.Factory.StartNew<IHttpActionResult>(() =>
                //{
                //    return this.ResponseMessage(Request.CreateResponse<Response>(HttpStatusCode.OK, resp));
                //});
            }
            catch (Exception ex)
            {
                new TechnicalException("Error metodo Buscar", ex, _configuration);
                resp.Code = HttpStatusCode.NotFound.ToString();
                resp.Message = string.Empty;
                resp.Data = "No es posible realizar la busqueda, por favor volver a intentar más tarde.";
                return resp;
            }
        }

        /// <summary>
        /// agrega documento en formato XML, a la base de datos del Solr
        /// </summary>
        /// <param name="xmlData"></param>
        /// <returns></returns>
        /// <response code="401">No Autorizado. No se ha iniciado sesión.</response>
        /// <response code="400">BadRequest. El protocolo de petición no es el correcto.</response>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [Route("Agregar")]
        public ActionResult<Response> Agregar(string xmlData)
        {
            Response resp = new Response();
            try
            {
                xmlData = xmlData.Replace("sgd_documento", "SolrXml");
                SolrXml solrXml = new SolrXml();
                solrXml = (SolrXml)FileBo.DeserializeXML(solrXml.GetType(), xmlData);

                string tagPattern = @"<[!--\W*?]*?[/]*?\w+.*?>";
                MatchCollection matches = Regex.Matches(solrXml.Texto, tagPattern);
                foreach (Match match in matches)
                {
                    solrXml.Texto = solrXml.Texto.Replace(match.Value, string.Empty).Replace("\n", "");
                }
                string texto = HttpUtility.HtmlDecode(solrXml.Texto);
                string xml = "<add><doc>";
                xml += "<field name=\"id\">" + solrXml.IdDocumento + "</field>";
                xml += "<field name=\"IdDocumento\">" + solrXml.IdDocumento + "</field>";
                xml += "<field name=\"Titulo\">" + solrXml.Titulo + "</field>";
                xml += "<field name=\"Descripcion\">" + solrXml.Descripcion + "</field>";
                xml += "<field name=\"FechaCreacion\">" + solrXml.FechaCreacion + "</field>";
                xml += "<field name=\"Version\">" + solrXml.Version + "</field>";
                xml += "<field name=\"Texto\">" + texto + "</field>";
                xml += "</doc></add>";

                string urlSolr = _configuration["webSolr"] + "/solr/test-2/update?commitWithin=1000&overwrite=true&wt=json";
                string responseStr = string.Empty;
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(urlSolr);
                byte[] bytes;
                bytes = System.Text.Encoding.UTF8.GetBytes(xml);
                request.ContentType = "text/xml; encoding='utf-8'";
                request.ContentLength = bytes.Length;
                request.Method = "POST";
                Stream requestStream = request.GetRequestStream();
                requestStream.Write(bytes, 0, bytes.Length);
                requestStream.Close();
                HttpWebResponse response;
                response = (HttpWebResponse)request.GetResponse();
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    Stream responseStream = response.GetResponseStream();
                    responseStr = new StreamReader(responseStream).ReadToEnd();
                }

                resp.Code = "OK";
                resp.Message = "Se agrego nuevo documento.";
                resp.Data = responseStr;

                return resp;
            }
            catch (Exception ex)
            {
                new TechnicalException("Error metodo Agregar", ex, _configuration);
                resp.Code = "NotFound";
                resp.Message = string.Empty;
                resp.Data = "No es posible agregar documento, por favor volver a intentar más tarde.";
                return resp;
            }

        }

        /// <summary>
        /// agrega documento en formato XML, a la base de datos del Solr
        /// </summary>
        /// <param name="ma">Objeto Medio ambiental</param>
        /// <param name="nuevo">si el documento es nuevo</param>
        /// <returns></returns>
        /// <response code="401">No Autorizado. No se ha iniciado sesión.</response>
        /// <response code="400">BadRequest. El protocolo de petición no es el correcto.</response>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [Route("AgregarMA")]
        public ActionResult<Response> AgregarMA(MedioAmbiental ma, bool nuevo)
        {
            Response resp = new Response();
            string responseStr = string.Empty;

            try
            {
                ma.Texto = Regex.Replace(ma.Texto, "[<].*?>", " ");
                ma.Texto = Regex.Replace(ma.Texto, @"\s+", " ");

                ma.Texto = DecodeHtmlText(ma.Texto);

                string xml = "";

                xml = "<add><doc>";
                if (!nuevo)
                {
                    xml += "<field name=\"id\">" + ma.id + "</field>";
                }
                xml += "<field name=\"ma_iddocumento\">" + ma.IdDocumento + "</field>";
                xml += "<field name=\"ma_categoria\">" + ma.Categoria + "</field>";
                xml += "<field name=\"ma_norma\">" + ma.Norma + "</field>";
                xml += "<field name=\"ma_numero\">" + ma.Numero + "</field>";
                xml += "<field name=\"ma_organismo\">" + ma.Organismo + "</field>";
                xml += "<field name=\"ma_seccion\">" + ma.Seccion + "</field>";
                xml += "<field name=\"ma_suborganismo\">" + ma.SubOrganismo + "</field>";
                xml += "<field name=\"ma_tema\">" + ma.Tema + "</field>";
                xml += "<field name=\"ma_texto\">" + ma.Texto + "</field>";
                xml += "<field name=\"ma_titulo\">" + ma.Titulo + "</field>";
                xml += "</doc></add>";

                string urlSolr = _configuration["webSolr"] + "/solr/test-2/update?commitWithin=1000&overwrite=true&wt=json";

                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(urlSolr);
                byte[] bytes;
                bytes = System.Text.Encoding.UTF8.GetBytes(xml);
                request.ContentType = "text/xml; encoding='utf-8'";
                request.ContentLength = bytes.Length;
                request.Method = "POST";
                Stream requestStream = request.GetRequestStream();
                requestStream.Write(bytes, 0, bytes.Length);
                requestStream.Close();
                HttpWebResponse response;
                response = (HttpWebResponse)request.GetResponse();
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    Stream responseStream = response.GetResponseStream();
                    responseStr = new StreamReader(responseStream).ReadToEnd();
                }
                resp.Code = "OK";
                resp.Message = "Se agrego nuevo documento Medio Ambiental.";
                resp.Data = responseStr;

                return resp;
            }
            catch (Exception ex)
            {
                new TechnicalException("Error metodo AgregarMA", ex, _configuration);
                resp.Code = "NotFound";
                resp.Message = string.Empty;
                resp.Data = "No es posible agregar documento Medio Ambiental, por favor volver a intentar más tarde.";
                return resp;
            }
        }

        /// <summary>
        /// Busca documento po id del Solr
        /// </summary>
        /// <param name="id"></param>
        /// <returns>devuelve objeto Json con información del documento.</returns>
        /// <response code="401">No Autorizado. No se ha iniciado sesión.</response>
        /// <response code="400">BadRequest. El protocolo de petición no es el correcto.</response>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [Route("QueryById")]
        public ActionResult<Response> QueryById(string id)
        {
            Response resp = new Response();
            string directorio_ma = _configuration["PATH_:key"];
            try
            {
                string urlSolr = _configuration["webSolr"];
                string url = urlSolr + "/solr/test-1/select?q=id%3A" + id + "%20OR%20IdDocumento%3A" + id;
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                HttpWebResponse response;
                response = (HttpWebResponse)request.GetResponse();

                string responseStr = "";
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    Stream responseStream = response.GetResponseStream();
                    responseStr = new StreamReader(responseStream).ReadToEnd();
                }

                var expConverter = new ExpandoObjectConverter();
                dynamic obj = JsonConvert.DeserializeObject<ExpandoObject>(responseStr, expConverter);
                string idDocumento = "";
                string id_ = "";
                string Coleccion = "";
                string Origen = "";
                string Fecha = "";
                string xml = "";
                foreach (var doc_ in obj.response.docs)
                {
                    foreach (var v in doc_.Coleccion)
                    {
                        Coleccion = v;
                    }
                    try
                    {
                        Origen = doc_.Origen;
                    }
                    catch { }
                    id_ = doc_.id;
                    idDocumento = doc_.IdDocumento;
                    Fecha = Convert.ToString(doc_.Fecha);
                    //Norma = (doc_.Norma).Replace(" ", "_") + "\\";
                }
                if (Origen == "")
                    Origen = Coleccion;
                else
                    Coleccion = Origen;

                if (Coleccion != "BITE" && Coleccion != "MA" && Coleccion != "LA")
                {
                    DateTime fechadoc = Convert.ToDateTime(Fecha);
                    Fecha = fechadoc.ToString("dd-MM-yyyy");
                    string[] f = Fecha.Split('-');
                    Fecha = f[2] + "\\" + f[1] + "\\" + f[0];
                    Coleccion = "DOE\\" + Fecha;
                }
                string ruta = directorio_ma + Coleccion + "\\" + idDocumento + ".xml";
                xml = System.IO.File.ReadAllText(ruta);


                resp.Code = "OK";
                resp.Message = "Búsqueda exitosa";
                resp.Data = xml;
                return resp;

            }
            catch (Exception ex)
            {
                new TechnicalException("Error metodo QueryById", ex, _configuration);
                resp.Code = "NotFound";
                resp.Message = string.Empty;
                resp.Data = "No es posible buscar por id de documento, por favor volver a intentar más tarde.";

                return resp;
            }
        }


        /// <summary>
        /// Busca documentos segun filtro entregado.
        /// </summary>
        /// <param name="form">Objeto con filtro para la busqueda.</param>
        /// <returns>Lista de aciertos segun criterios entregados.</returns>
        /// <response code="401">No Autorizado. No se ha iniciado sesión.</response>
        /// <response code="400">BadRequest. El protocolo de petición no es el correcto.</response>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [Route("Filtro")]
        public ActionResult<Response> Filtro(FormularioBusqueda form)
        {
            Response resp = new Response();

            string borrador = form.Borrador;
            string pendiente = form.Pendiente;
            string ct = form.Ct;
            string lr = form.Lr;
            string lt = form.Lt;
            string lzf = form.Lzf;
            string liva = form.Liva;

            string circulares = form.Circular;
            string decretos = form.Decreto;
            string dfl = form.Dfl;
            string dl = form.Dl;
            string ds = form.Ds;
            string ley = form.Ley;
            string resolucion = form.Resolucion;
            string fecha = form.Fecha;
            string numero = form.Numero;
            string articulo = form.Articulo;
            string inciso = form.Inciso;
            string texto = form.Texto;
            string pagina = Convert.ToString(form.Pagina);
            string coleccion = form.Coleccion;

            coleccion = "&q=Coleccion:'" + coleccion + "'";
            bool ordenBy = false;

            string q = "";
            try
            {
                string bNorma = "";
                string bDatos = "";
                if (borrador != "")
                {
                    coleccion = "";
                    //bDatos = "&q=Estado:'99'"; // borrador
                }
                else if (pendiente != "")
                {
                    coleccion = "";
                    //bDatos = "&q=Estado:'98'"; // pendiente
                }
                else
                {
                    #region filtros
                    if (ct != "")
                    {
                        q += "Norma:'CODIGO TRIBUTARIO'";
                        ordenBy = true;
                    }
                    if (lr != "")
                    {
                        q += (q != "" && q != "") ? " OR Norma:'LEY DE LA RENTA'" : "Norma:'LEY DE LA RENTA'";
                        ordenBy = true;
                    }
                    if (lt != "")
                    {
                        q += (q != "" && q != "") ? " OR Norma:'LEY DE TIMBRES Y ESTAMPILLAS'" : "Norma:'LEY DE TIMBRES Y ESTAMPILLAS'";
                        ordenBy = true;
                    }
                    if (lzf != "")
                    {
                        q += (q != "" && q != "") ? " OR Norma:'LEY DE ZONA FRANCA'" : "Norma:'LEY DE ZONA FRANCA'";
                        ordenBy = true;
                    }
                    if (liva != "")
                    {
                        q += (q != "" && q != "") ? " OR Norma:'LEY DEL IVA'" : "Norma:'LEY DEL IVA'";
                        ordenBy = true;
                    }
                    if (circulares != "")
                        q += (q != "" && q != "") ? " OR Norma:'CIRCULAR'" : "Norma:'CIRCULAR'";
                    if (decretos != "")
                        q += (q != "" && q != "") ? " OR Norma:'DECRETO'" : "Norma:'DECRETO'";
                    if (dfl != "")
                        q += (q != "" && q != "") ? " OR Norma:'DECRETO CON FUERZA DE LEY'" : "Norma:'DECRETO CON FUERZA DE LEY'";
                    if (dl != "")
                        q += (q != "" && q != "") ? " OR Norma:'DECRETO LEY'" : "Norma:'DECRETO LEY'";
                    if (ds != "")
                        q += (q != "" && q != "") ? " OR Norma:'DECRETO SUPREMO'" : "Norma:'DECRETO SUPREMO'";
                    if (ley != "")
                        q += (q != "" && q != "") ? " OR Norma:'LEY'" : "Norma:'LEY'";
                    if (resolucion != "")
                        q += (q != "" && q != "") ? " OR Norma:'RESOLUCION'" : "Norma:'RESOLUCION'";

                    bNorma = "";
                    if (q != "")
                        bNorma = " AND (" + q + ")";

                    q = "";
                    if (!String.IsNullOrEmpty(fecha))
                    {
                        fecha = fecha.Replace("/", "-");
                        string[] f = fecha.Split('-');
                        fecha = f[2] + "-" + f[1] + "-" + f[0] + @"T00:00:00Z";
                        q += " AND Fecha:'" + fecha + "'";
                    }
                    if (numero != "")
                        q += " AND Numero:'" + numero + "'";
                    if (articulo != "")
                        q += " AND Articulo:'" + articulo + "'";
                    if (inciso != "")
                        q += " AND Inciso:'" + inciso + "'";
                    if (texto != "")
                        q += " AND Texto:'" + texto + "'";

                    bDatos = "";
                    if (q != "")
                        bDatos = q;
                }
                #endregion

                string fl = "Norma,Numero,Articulo,Inciso,Titulo,Fecha,IdDocumento,Organismo,Estado,Partes,Tribunal,Propiedad";

                string orden = "";
                if (ordenBy)
                    orden = "Orden";
                else
                    orden = "Fecha";

                string url = "select?fl=" + fl + coleccion + bNorma + bDatos + "&sort=" + orden + " asc&start=" + pagina;
                url = url.Replace("  ", " ");
                url = url.Replace(",", "%2C").Replace(" ", "%20").Replace(":", "%3A").Replace("'", "%22");
                string fUrl = _configuration["webSolr"] + "/solr/test-1/" + url;
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(fUrl);
                HttpWebResponse response;
                response = (HttpWebResponse)request.GetResponse();

                string responseStr = "";
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    Stream responseStream = response.GetResponseStream();
                    responseStr = new StreamReader(responseStream).ReadToEnd();
                }

                var expConverter = new ExpandoObjectConverter();
                dynamic obj = JsonConvert.DeserializeObject<ExpandoObject>(responseStr, expConverter);

                resp.Code = "OK";
                resp.Message = "Búsqueda exitosa";
                resp.Data = JsonConvert.SerializeObject(obj.response);

                return resp;

            }
            catch (Exception ex)
            {
                new TechnicalException("Error metodo Filtro", ex, _configuration);
                resp.Code = "NotFound";
                resp.Message = string.Empty;
                resp.Data = "No es posible buscar por filtro, por favor volver a intentar más tarde.";

                return resp;
            }
        }

        /// <summary>
        /// Busca documentos en normas generales segun filtro entregado.
        /// </summary>
        /// <param name="nor">Objeto con filtro para la busqueda.</param>
        /// <returns>Lista de aciertos segun criterios entregados.</returns>
        /// <response code="401">No Autorizado. No se ha iniciado sesión.</response>
        /// <response code="400">BadRequest. El protocolo de petición no es el correcto.</response>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [Route("BuscarNG")]
        public ActionResult<Response> BuscarNG(NormasGenerales nor)
        {
            Response resp = new Response();
            try
            {
                string q = string.Empty;
                string bNorma = string.Empty;
                string bDatos = string.Empty;
                string fecha = string.Empty;
                string fecha2 = string.Empty;
                string coleccion = "&q=Coleccion:'DONG'";
                string fl = "Norma,Numero,Articulo,Inciso,Titulo,Fecha,IdDocumento,Organismo,Estado,Partes,Tribunal,Propiedad";

                if (!String.IsNullOrEmpty(nor.LY))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Norma:'LEY'" : "Norma:'LEY' ";
                if (!String.IsNullOrEmpty(nor.RES))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Norma:'RESOLUCION'" : "Norma:'RESOLUCION' ";
                if (!String.IsNullOrEmpty(nor.CI))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Norma:'CIRCULAR'" : "Norma:'CIRCULAR' ";
                if (!String.IsNullOrEmpty(nor.DF))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Norma:'DFL'" : "Norma:'DFL' ";
                if (!String.IsNullOrEmpty(nor.PJ))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Norma:'NOMINA'" : "Norma:'NOMINA' ";
                if (!String.IsNullOrEmpty(nor.AA))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Norma:'AAC'" : "Norma:'AAC' ";
                if (!String.IsNullOrEmpty(nor.ACBC))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Norma:'ACUERDO' OR Norma:'CERTIFICADO'" : "Norma:'ACUERDO' OR Norma:'CERTIFICADO' ";
                if (!String.IsNullOrEmpty(nor.DO))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Norma:'DCTO' Norma:'ORDENANZA'" : "Norma:'DCTO' Norma:'ORDENANZA'";
                if (!String.IsNullOrEmpty(nor.IG))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Norma:'INFORMACION' OR Norma:'COREDE' OR Norma:'INSTRUCTIVO' OR Norma:'NORMA' Norma:'ORDEN' OR Norma:'SENTENCIA' OR Norma:'RECTIFICACION' OR Norma:'REGLAMENTO'" : "Norma:'INFORMACION' OR Norma:'COREDE' OR Norma:'INSTRUCTIVO' OR Norma:'NORMA' Norma:'ORDEN' OR Norma:'SENTENCIA' OR Norma:'RECTIFICACION' OR Norma:'REGLAMENTO'";

                if (!String.IsNullOrEmpty(q))
                    bNorma = " AND (" + q + ")";

                q = string.Empty;

                if (!string.IsNullOrEmpty(nor.FechaD))
                {
                    fecha = nor.FechaD.Replace("/", "-");
                    DateTime fechadoc = Convert.ToDateTime(fecha);
                    fecha = fechadoc.ToString("dd-MM-yyyy");
                    string[] f = fecha.Split('-');
                    fecha = f[2] + "-" + f[1] + "-" + f[0] + @"T00:00:00Z";


                    if (!string.IsNullOrEmpty(nor.FechaH))
                    {
                        fecha2 = nor.FechaH.Replace("/", "-");
                        fechadoc = Convert.ToDateTime(fecha2);
                        fecha2 = fechadoc.ToString("dd-MM-yyyy");
                        f = fecha2.Split('-');
                        fecha2 = f[2] + "-" + f[1] + "-" + f[0] + @"T00:00:00Z";
                        q += " AND Fecha:[" + fecha + " TO " + fecha2 + "]";
                    }
                    else
                    {
                        q += " AND Fecha:'" + fecha + "'";
                    }

                }

                if (!String.IsNullOrEmpty(nor.exacta))
                {
                    q += " AND Texto:'" + nor.exacta + "'";
                }

                if (!string.IsNullOrEmpty(nor.num))
                    q += " AND Numero:'" + nor.num + "'";

                if (!string.IsNullOrEmpty(nor.todas))
                {
                    if (!string.IsNullOrEmpty(nor.ninguna))
                        q += " AND Texto:'" + nor.todas + "' AND NOT Texto:'" + nor.ninguna + "'";
                    else
                        q += " AND Texto:'" + nor.todas + "'";
                }

                if (!string.IsNullOrEmpty(nor.ninguna))
                    q += " AND NOT Texto :'" + nor.ninguna + "'";

                if (!string.IsNullOrEmpty(nor.plus))
                    q += " AND Texto:'*" + nor.plus + "*'";

                if (q != "")
                    bDatos = q;

                string destacado = "&hl.fl=Texto&hl.simple.post=<%2Fspan>&hl.simple.pre=<span%20class%3D%27MatchDestacado%27>&hl=on";

                string url = "select?fl=" + fl + coleccion + bNorma + bDatos + destacado + "&sort=Fecha asc &start=" + nor.Pagina;
                url = url.Replace("  ", " ");
                url = url.Replace(",", "%2C").Replace(" ", "%20").Replace(":", "%3A").Replace("'", "%22");

                string fUrl = _configuration["webSolr"] + "/solr/test-1/" + url;
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(fUrl);
                HttpWebResponse response;
                response = (HttpWebResponse)request.GetResponse();

                string responseStr = "";
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    Stream responseStream = response.GetResponseStream();
                    responseStr = new StreamReader(responseStream).ReadToEnd();
                }

                var expConverter = new ExpandoObjectConverter();
                dynamic obj = JsonConvert.DeserializeObject<ExpandoObject>(responseStr, expConverter);

                resp.Code = "OK";
                resp.Message = "Búsqueda exitosa";
                resp.Data = JsonConvert.SerializeObject(obj.response);
                resp.highlighting = JsonConvert.SerializeObject(obj.highlighting);

                return resp;

            }
            catch (Exception ex)
            {
                new TechnicalException("Error metodo Filtro", ex, _configuration);
                resp.Code = "NotFound";
                resp.Message = string.Empty;
                resp.Data = "No es posible buscar por filtro, por favor volver a intentar más tarde.";

                return resp;
            }
        }

        /// <summary>
        /// Busca documentos en normas particulares segun filtro entregado.
        /// </summary>
        /// <param name="nor">Objeto con filtro para la busqueda.</param>
        /// <returns>Lista de aciertos segun criterios entregados.</returns>
        /// <response code="401">No Autorizado. No se ha iniciado sesión.</response>
        /// <response code="400">BadRequest. El protocolo de petición no es el correcto.</response>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [Route("BuscarNP")]
        public ActionResult<Response> BuscarNP(NormasPerticulares nor)
        {
            Response resp = new Response();
            try
            {
                string q = string.Empty;
                string bNorma = string.Empty;
                string bDatos = string.Empty;
                string fecha = string.Empty;
                string fecha2 = string.Empty;
                string coleccion = "&q=Coleccion:'DONP'";
                string fl = "Norma,Numero,Articulo,Inciso,Titulo,Fecha,IdDocumento,Organismo,Estado,Partes,Tribunal,Propiedad";

                if (!String.IsNullOrEmpty(nor.dc))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Norma:'DECRETO'" : "Norma:'DECRETO'";
                if (!String.IsNullOrEmpty(nor.soc))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Norma:'SOCIEDAD'" : "Norma:'SOCIEDAD'";
                if (!String.IsNullOrEmpty(nor.pre))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Norma:'PRENDA'" : "Norma:'PRENDA'";
                if (!String.IsNullOrEmpty(nor.da))
                {
                    q += (!String.IsNullOrEmpty(q)) ? " OR Norma:'SOLICITUD EXPLORACION' OR Norma:'SOLICITUD INSCRIPCION' OR Norma:'SOLICITUD REGULARIZACION'" : "Norma:'RESOLUCION' OR Norma:'SOLICITUD INSCRIPCION' OR Norma:'SOLICITUD REGULARIZACION'";
                }
                if (!String.IsNullOrEmpty(nor.dc))
                {
                    q += (!String.IsNullOrEmpty(q)) ? " OR Norma:'PROTOCOLIZACION' OR Norma:'INFORMACION SORTEO' OR Norma:'CERTIFICACION' OR Norma:'CERTIFICADO' OR Norma:'LISTA DCTO' OR Norma:'IMPACTO AMBIENTAL' OR Norma:'PLANTA RESIDUOS' OR Norma:'ACUERDO' OR Norma:'REGISTRO VARIEDAD' OR Norma:'LISTA NOMBRAMIENTO' OR Norma:'PARTIDO'" : "Norma:'PROTOCOLIZACION' OR Norma:'INFORMACION SORTEO' OR Norma:'CERTIFICACION' OR Norma:'CERTIFICADO' OR Norma:'LISTA DCTO' OR Norma:'IMPACTO AMBIENTAL' OR Norma:'PLANTA RESIDUOS' OR Norma:'ACUERDO' OR Norma:'REGISTRO VARIEDAD' OR Norma:'LISTA NOMBRAMIENTO' OR Norma:'PARTIDO'";
                }
                if (!String.IsNullOrEmpty(nor.res))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Norma:'RESOLUCION'" : "Norma:'RESOLUCION'";
                if (!String.IsNullOrEmpty(nor.eirl))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Norma:'EMPRESA INDIVIDUAL DISOLUCION' OR Norma:'EMPRESA INDIVIDUAL CONSTITUCION' OR Norma:'EMPRESA INDIVIDUAL DISOLUCION' OR Norma:'EMPRESA INDIVIDUAL MODIFICACION'" : "Norma:'EMPRESA INDIVIDUAL DISOLUCION' OR Norma:'EMPRESA INDIVIDUAL CONSTITUCION' OR Norma:'EMPRESA INDIVIDUAL DISOLUCION' OR Norma:'EMPRESA INDIVIDUAL MODIFICACION'";
                if (!String.IsNullOrEmpty(nor.mc))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Norma:'MARCA'" : "Norma:'MARCA'";
                if (!String.IsNullOrEmpty(nor.spo))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Norma:'SOLICITUD TELECOMUNICACIONE' OR Norma:'OTRAS SOLICITUDES' OR Norma:'SOLICITUD TRANSPORTES' OR Norma:'SOLICITUD CONCESION' OR Norma:'SOLICITUD CONCESION TELEVISIVA' OR Norma:'SOLICITUD ELECTRICIDAD'" : "Norma:'SOLICITUD TELECOMUNICACIONE' OR Norma:'OTRAS SOLICITUDES' OR Norma:'SOLICITUD TRANSPORTES' OR Norma:'SOLICITUD CONCESION' OR Norma:'SOLICITUD CONCESION TELEVISIVA' OR Norma:'SOLICITUD ELECTRICIDAD'";
                if (!String.IsNullOrEmpty(nor.ag))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Norma:'ASOCIACION GREMIAL'" : "Norma:'ASOCIACION GREMIAL'";
                if (!String.IsNullOrEmpty(nor.pmd))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Norma:'PATENTE' OR Norma:'MODELO' OR Norma:'DISEÑO'" : "Norma:'DECRETO' OR Norma:'MODELO' OR Norma:'DISEÑO'";
                if (!String.IsNullOrEmpty(nor.pm))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Norma:'ART 83' OR Norma:'PEDIMENTOS MINEROS' OR Norma:'MANIFESTACIONES MINERAS' OR Norma:'SOLICITUDES DE MENSURA' OR Norma:'SENTENCIA EXPLORACION' OR Norma:'SENTENCIA EXPLOTACION' OR Norma:'VIGENCIA MENSURA' OR Norma:'RENUNCIA CONCESION' OR Norma:'PRORROGA EXPLORACION' OR Norma:'NOMINA REMATE' OR Norma:'ACUERDO JUNTA' OR Norma:'CITACION JUNTA' OR Norma:'NOMINA PATENTE' OR Norma:'ACUERDO CONCESION'" : "Norma:'ART 83' OR Norma:'PEDIMENTOS MINEROS' OR Norma:'MANIFESTACIONES MINERAS' OR Norma:'SOLICITUDES DE MENSURA' OR Norma:'SENTENCIA EXPLORACION' OR Norma:'SENTENCIA EXPLOTACION' OR Norma:'VIGENCIA MENSURA' OR Norma:'RENUNCIA CONCESION' OR Norma:'PRORROGA EXPLORACION' OR Norma:'NOMINA REMATE' OR Norma:'ACUERDO JUNTA' OR Norma:'CITACION JUNTA' OR Norma:'NOMINA PATENTE' OR Norma:'ACUERDO CONCESION'";
                if(!String.IsNullOrEmpty(nor.ot))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Norma:'PROTOCOLIZACION' OR Norma:'INFORMACION SORTEO' OR Norma:'CERTIFICACION' OR Norma:'CERTIFICADO' OR Norma:'LISTA DCTO' OR Norma:'IMPACTO AMBIENTAL' OR Norma:'PLANTA RESIDUOS' OR Norma:'ACUERDO' OR Norma:'REGISTRO VARIEDAD' OR Norma:'LISTA NOMBRAMIENTO' OR Norma:'PARTIDO'" : "Norma:'PROTOCOLIZACION' OR Norma:'INFORMACION SORTEO' OR Norma:'CERTIFICACION' OR Norma:'CERTIFICADO' OR Norma:'LISTA DCTO' OR Norma:'IMPACTO AMBIENTAL' OR Norma:'PLANTA RESIDUOS' OR Norma:'ACUERDO' OR Norma:'REGISTRO VARIEDAD' OR Norma:'LISTA NOMBRAMIENTO' OR Norma:'PARTIDO'";


                if (!String.IsNullOrEmpty(q))
                    bNorma = " AND (" + q + ")";

                q = string.Empty;

                if (!string.IsNullOrEmpty(nor.FechaD))
                {
                    fecha = nor.FechaD.Replace("/", "-");
                    DateTime fechadoc = Convert.ToDateTime(fecha);
                    fecha = fechadoc.ToString("dd-MM-yyyy");
                    string[] f = fecha.Split('-');
                    fecha = f[2] + "-" + f[1] + "-" + f[0] + @"T00:00:00Z";


                    if (!string.IsNullOrEmpty(nor.FechaH))
                    {
                        fecha2 = nor.FechaH.Replace("/", "-");
                        fechadoc = Convert.ToDateTime(fecha2);
                        fecha2 = fechadoc.ToString("dd-MM-yyyy");
                        f = fecha2.Split('-');
                        fecha2 = f[2] + "-" + f[1] + "-" + f[0] + @"T00:00:00Z";
                        q += " AND Fecha:[" + fecha + " TO " + fecha2 + "]";
                    }
                    else
                    {
                        q += " AND Fecha:'" + fecha + "'";
                    }
                }

                if (!string.IsNullOrEmpty(nor.n))
                    q += " AND Numero:'" + nor.n + "'";

                if (!String.IsNullOrEmpty(nor.exacta))
                {
                    q += " AND Texto:'" + nor.exacta + "'";
                }

                if (!string.IsNullOrEmpty(nor.Todas))
                {
                    if (!string.IsNullOrEmpty(nor.ninguna))
                        q += " AND Texto:'" + nor.Todas + "' AND NOT Texto:'" + nor.ninguna + "'";
                    else
                        q += " AND Texto:'" + nor.Todas + "'";
                }

                if (!string.IsNullOrEmpty(nor.plus))
                    q += " AND Texto:'*" + nor.plus + "*'";

                if (!string.IsNullOrEmpty(nor.ninguna))
                    q += " AND NOT Texto :'" + nor.ninguna + "'";

                string destacado = "&hl.fl=Texto&hl.simple.post=<%2Fspan>&hl.simple.pre=<span%20class%3D%27MatchDestacado%27>&hl=on";

                string url = "select?fl=" + fl + coleccion + bNorma + bDatos + destacado + "&sort=Fecha asc &start=" + nor.Pagina;
                url = url.Replace("  ", " ");
                url = url.Replace(",", "%2C").Replace(" ", "%20").Replace(":", "%3A").Replace("'", "%22");
                string fUrl = _configuration["webSolr"] + "/solr/test-1/" + url;
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(fUrl);
                HttpWebResponse response;
                response = (HttpWebResponse)request.GetResponse();

                string responseStr = "";
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    Stream responseStream = response.GetResponseStream();
                    responseStr = new StreamReader(responseStream).ReadToEnd();
                }

                var expConverter = new ExpandoObjectConverter();
                dynamic obj = JsonConvert.DeserializeObject<ExpandoObject>(responseStr, expConverter);

                resp.Code = "OK";
                resp.Message = "Búsqueda exitosa";
                resp.Data = JsonConvert.SerializeObject(obj.response);
                resp.highlighting = JsonConvert.SerializeObject(obj.highlighting);

                return resp;

            }
            catch (Exception ex)
            {
                new TechnicalException("Error metodo Filtro", ex, _configuration);
                resp.Code = "NotFound";
                resp.Message = string.Empty;
                resp.Data = "No es posible buscar por filtro, por favor volver a intentar más tarde.";

                return resp;
            }
        }

        /// <summary>
        /// Busca documentos en publicaciones judiciales segun filtro entregado.
        /// </summary>
        /// <param name="nor">Objeto con filtro para la busqueda.</param>
        /// <returns>Lista de aciertos segun criterios entregados.</returns>
        /// <response code="401">No Autorizado. No se ha iniciado sesión.</response>
        /// <response code="400">BadRequest. El protocolo de petición no es el correcto.</response>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [Route("BuscarPJ")]
        public ActionResult<Response> BuscarPJ(PublicacionesJudiciales nor)
        {
            Response resp = new Response();
            try
            {
                string q = string.Empty;
                string bNorma = string.Empty;
                string bDatos = string.Empty;
                string fecha = string.Empty;
                string fecha2 = string.Empty;
                string coleccion = "&q=Coleccion:'DOPJ'";
                string fl = "Norma,Numero,Articulo,Inciso,Titulo,Fecha,IdDocumento,Organismo,Estado,Partes,Tribunal,Propiedad";

                if (!String.IsNullOrEmpty(nor.qu))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Norma:'QUIEBRA'" : "Norma:'QUIEBRA'";
                if (!String.IsNullOrEmpty(nor.cm))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Norma:'CAMBIO NOMBRE'" : "Norma:'CAMBIO NOMBRE'";
                if (!String.IsNullOrEmpty(nor.mp))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Norma:'MUERTE PRESUNTA'" : "Norma:'MUERTE PRESUNTA'";
                if (!String.IsNullOrEmpty(nor.n))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Norma:'NOTIFICACION'" : "Norma:'NOTIFICACION'";
                if (!String.IsNullOrEmpty(nor.ed))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Norma:'EXTRAVIO'" : "Norma:'EXTRAVIO'";
                if (!String.IsNullOrEmpty(nor.rd))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Norma:'DOMINIO'" : "Norma:'DOMINIO'";



                if (!String.IsNullOrEmpty(q))
                    bNorma = " AND (" + q + ")";

                q = string.Empty;

                if (!string.IsNullOrEmpty(nor.FechaD))
                {
                    fecha = nor.FechaD.Replace("/", "-");
                    string[] f = fecha.Split('-');
                    fecha = f[2] + "-" + f[1] + "-" + f[0] + @"T00:00:00Z";
                    fecha2 = nor.FechaH.Replace("/", "-");
                    f = fecha2.Split('-');
                    fecha2 = f[2] + "-" + f[1] + "-" + f[0] + @"T00:00:00Z";
                    q += " AND Fecha:'" + fecha + " A " + fecha2 + "'";
                }

                if (!String.IsNullOrEmpty(nor.exacta))
                {
                    q += " AND Texto:'" + nor.exacta + "'";
                }

                if (!string.IsNullOrEmpty(nor.Todas))
                {
                    if (!string.IsNullOrEmpty(nor.ninguna))
                        q += " AND Texto:'" + nor.Todas + " NOT " + nor.ninguna + "'";
                    else
                        q += " AND Texto:'" + nor.Todas + "'";
                }

                if (!string.IsNullOrEmpty(nor.plus))
                    q += " AND Texto:'*" + nor.plus + "*'";

                string destacado = "&hl.fl=Texto&hl.simple.post=<%2Fspan>&hl.simple.pre=<span%20class%3D%27MatchDestacado%27>&hl=on";

                string url = "select?fl=" + fl + coleccion + bNorma + bDatos + destacado + "&sort=Fecha asc &start=" + nor.pagina;
                url = url.Replace("  ", " ");
                url = url.Replace(",", "%2C").Replace(" ", "%20").Replace(":", "%3A").Replace("'", "%22");
                string fUrl = _configuration["webSolr"] + "/solr/test-1/" + url;
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(fUrl);
                HttpWebResponse response;
                response = (HttpWebResponse)request.GetResponse();

                string responseStr = "";
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    Stream responseStream = response.GetResponseStream();
                    responseStr = new StreamReader(responseStream).ReadToEnd();
                }

                var expConverter = new ExpandoObjectConverter();
                dynamic obj = JsonConvert.DeserializeObject<ExpandoObject>(responseStr, expConverter);

                resp.Code = "OK";
                resp.Message = "Búsqueda exitosa";
                resp.Data = JsonConvert.SerializeObject(obj.response);
                resp.highlighting = JsonConvert.SerializeObject(obj.highlighting);

                return resp;

            }
            catch (Exception ex)
            {
                new TechnicalException("Error metodo Filtro", ex, _configuration);
                resp.Code = "NotFound";
                resp.Message = string.Empty;
                resp.Data = "No es posible buscar por filtro, por favor volver a intentar más tarde.";

                return resp;
            }
        }

        /// <summary>
        /// Busca documentos en avisos segun filtro entregado.
        /// </summary>
        /// <param name="nor">Objeto con filtro para la busqueda.</param>
        /// <returns>Lista de aciertos segun criterios entregados.</returns>
        /// <response code="401">No Autorizado. No se ha iniciado sesión.</response>
        /// <response code="400">BadRequest. El protocolo de petición no es el correcto.</response>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [Route("BuscarA")]
        public ActionResult<Response> BuscarA(Avisos nor)
        {
            Response resp = new Response();
            try
            {
                string q = string.Empty;
                string bNorma = string.Empty;
                string bDatos = string.Empty;
                string fecha = string.Empty;
                string fecha2 = string.Empty;
                string coleccion = "&q=Coleccion:'DOAV'";
                string fl = "Norma,Numero,Articulo,Inciso,Titulo,Fecha,IdDocumento,Organismo,Estado,Partes,Tribunal,Propiedad";

                if (!String.IsNullOrEmpty(nor.b))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Norma:'BALANCE'" : "Norma:'BALANCE'";
                if (!String.IsNullOrEmpty(nor.ca))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Norma:'CITACION' OR Norma:'REPARTO'" : "Norma:'CITACION' OR Norma:'REPARTO'";
                if (!String.IsNullOrEmpty(nor.cp))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Norma:'CONCURSO'" : "Norma:'CONCURSO'";
                if (!String.IsNullOrEmpty(nor.l))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Norma:'LICITACION'" : "Norma:'LICITACION'";
                if (!String.IsNullOrEmpty(nor.oa))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Norma:'SUBASTA' OR Norma:'REPARTO' OR Norma:'PAGO' OR Norma:'POSTULANTES' OR Norma:'INFORMACION' OR Norma:'SORTEO' OR Norma:'RIESGO' OR Norma:'EMISION' OR Norma:'ADJUDICACION'" : "Norma:'SUBASTA' OR Norma:'REPARTO' OR Norma:'PAGO' OR Norma:'POSTULANTES' OR Norma:'INFORMACION' OR Norma:'SORTEO' OR Norma:'RIESGO' OR Norma:'EMISION' OR Norma:'ADJUDICACION'";
                if (!String.IsNullOrEmpty(nor.p))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Norma:'PROPUESTA'" : "Norma:'PROPUESTA'";

                if (!String.IsNullOrEmpty(q))
                    bNorma = " AND (" + q + ")";

                q = string.Empty;

                if (!string.IsNullOrEmpty(nor.FechaD))
                {
                    fecha = nor.FechaD.Replace("/", "-");
                    DateTime fechadoc = Convert.ToDateTime(fecha);
                    fecha = fechadoc.ToString("dd-MM-yyyy");
                    string[] f = fecha.Split('-');
                    fecha = f[2] + "-" + f[1] + "-" + f[0] + @"T00:00:00Z";


                    if (!string.IsNullOrEmpty(nor.FechaH))
                    {
                        fecha2 = nor.FechaH.Replace("/", "-");
                        fechadoc = Convert.ToDateTime(fecha2);
                        fecha2 = fechadoc.ToString("dd-MM-yyyy");
                        f = fecha2.Split('-');
                        fecha2 = f[2] + "-" + f[1] + "-" + f[0] + @"T00:00:00Z";
                        q += " AND Fecha:[" + fecha + " TO " + fecha2 + "]";
                    }
                    else
                    {
                        q += " AND Fecha:'" + fecha + "'";
                    }

                }

                if (!String.IsNullOrEmpty(nor.exacta))
                {
                    q += " AND Texto:'" + nor.exacta + "'";
                }

                if (!string.IsNullOrEmpty(nor.Todas))
                {
                    if (!string.IsNullOrEmpty(nor.ninguna))
                        q += " AND Texto:'" + nor.Todas + "' AND NOT Texto:'" + nor.ninguna + "'";
                    else
                        q += " AND Texto:'" + nor.Todas + "'";
                }

                if (!string.IsNullOrEmpty(nor.ninguna))
                    q += " AND NOT Texto :'" + nor.ninguna + "'";

                if (!string.IsNullOrEmpty(nor.plus))
                    q += " AND Texto:'*" + nor.plus + "*'";

                string destacado = "&hl.fl=Texto&hl.simple.post=<%2Fspan>&hl.simple.pre=<span%20class%3D%27MatchDestacado%27>&hl=on";

                string url = "select?fl=" + fl + coleccion + bNorma + bDatos + destacado + "&sort=Fecha asc &start=" + nor.pagina;
                url = url.Replace("  ", " ");
                url = url.Replace(",", "%2C").Replace(" ", "%20").Replace(":", "%3A").Replace("'", "%22");
                string fUrl = _configuration["webSolr"] + "/solr/test-1/" + url;
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(fUrl);
                HttpWebResponse response;
                response = (HttpWebResponse)request.GetResponse();

                string responseStr = "";
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    Stream responseStream = response.GetResponseStream();
                    responseStr = new StreamReader(responseStream).ReadToEnd();
                }

                var expConverter = new ExpandoObjectConverter();
                dynamic obj = JsonConvert.DeserializeObject<ExpandoObject>(responseStr, expConverter);

                resp.Code = "OK";
                resp.Message = "Búsqueda exitosa";
                resp.Data = JsonConvert.SerializeObject(obj.response);
                resp.highlighting = JsonConvert.SerializeObject(obj.highlighting);

                return resp;

            }
            catch (Exception ex)
            {
                new TechnicalException("Error metodo Filtro", ex, _configuration);
                resp.Code = "NotFound";
                resp.Message = string.Empty;
                resp.Data = "No es posible buscar por filtro, por favor volver a intentar más tarde.";

                return resp;
            }
        }

        /// <summary>
        /// Busca documentos en Legislación Actualizada segun filtro entregado.
        /// </summary>
        /// <param name="nor">Objeto con filtro para la busqueda.</param>
        /// <returns>Lista de aciertos segun criterios entregados.</returns>
        /// <response code="401">No Autorizado. No se ha iniciado sesión.</response>
        /// <response code="400">BadRequest. El protocolo de petición no es el correcto.</response>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [Route("BuscarLA")]
        public ActionResult<Response> BuscarLA(LegislacionActualizada nor)
        {
            Response resp = new Response();
            try
            {
                string q = string.Empty;
                string bNorma = string.Empty;
                string bDatos = string.Empty;
                string fecha = string.Empty;
                string fecha2 = string.Empty;
                string coleccion = "&q=Coleccion:'LA'";
                string fl = "Norma,Numero,Articulo,Inciso,Titulo,Fecha,IdDocumento,Organismo,Estado,Partes,Tribunal,Propiedad";

                if (!String.IsNullOrEmpty(nor.dl))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Norma:'DL'" : "Norma:'DL'";
                if (!String.IsNullOrEmpty(nor.dfl))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Norma:'DFL'" : "Norma:'DFL'";
                if (!String.IsNullOrEmpty(nor.dec))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Norma:'DECRETO'" : "Norma:'DECRETO'";
                if (!String.IsNullOrEmpty(nor.rg))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Norma:'REGLAMENTO*'" : "Norma:'REGLAMENTO*'";
                if (!String.IsNullOrEmpty(nor.res))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Norma:'RESOLUCION'" : "Norma:'RESOLUCION'";
                if (!String.IsNullOrEmpty(nor.ti))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Norma:'convencion*'" : "Norma:'convencion*'";
                if (!String.IsNullOrEmpty(nor.ly))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Norma:'LEY'" : "Norma:'LEY'";
                if (!String.IsNullOrEmpty(nor.aac))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Norma:'aac'" : "Norma:'aac'";
                if (!String.IsNullOrEmpty(nor.ac))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Norma:'ACUERDO'" : "Norma:'ACUERDO'";
                if (!String.IsNullOrEmpty(nor.ci))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Norma:'CIRCULAR'" : "Norma:'CIRCULAR'";



                if (!String.IsNullOrEmpty(q))
                    bNorma = " AND (" + q + ")";

                q = string.Empty;

                if (!string.IsNullOrEmpty(nor.FechaD))
                {
                    fecha = nor.FechaD.Replace("/", "-");
                    DateTime fechadoc = Convert.ToDateTime(fecha);
                    fecha = fechadoc.ToString("dd-MM-yyyy");
                    string[] f = fecha.Split('-');
                    fecha = f[2] + "-" + f[1] + "-" + f[0] + @"T00:00:00Z";


                    if (!string.IsNullOrEmpty(nor.FechaH))
                    {
                        fecha2 = nor.FechaH.Replace("/", "-");
                        fechadoc = Convert.ToDateTime(fecha2);
                        fecha2 = fechadoc.ToString("dd-MM-yyyy");
                        f = fecha2.Split('-');
                        fecha2 = f[2] + "-" + f[1] + "-" + f[0] + @"T00:00:00Z";
                        q += " AND Fecha:[" + fecha + " TO " + fecha2 + "]";
                    }
                    else
                    {
                        q += " AND Fecha:'" + fecha + "'";
                    }

                }


                if (!String.IsNullOrEmpty(nor.exacta))
                {
                    q += " AND Texto:'" + nor.exacta + "'";
                }

                if (!string.IsNullOrEmpty(nor.todas))
                {
                    if (!string.IsNullOrEmpty(nor.ninguna))
                        q += " AND Texto:'" + nor.todas + "' AND NOT Texto:'" + nor.ninguna + "'";
                    else
                        q += " AND Texto:'" + nor.todas + "'";
                }

                if (!string.IsNullOrEmpty(nor.ninguna))
                    q += " AND NOT Texto :'" + nor.ninguna + "'";

                if (!string.IsNullOrEmpty(nor.plus))
                    q += " AND Texto:'*" + nor.plus + "*'";

                if (!string.IsNullOrEmpty(nor.norma))
                    q += " AND Norma:'" + nor.norma + "'";
                if (!string.IsNullOrEmpty(nor.n))
                    q += " AND Numero:'" + nor.n + "'";
                if (!string.IsNullOrEmpty(nor.articulo))
                    q += " AND Articulo:'" + nor.articulo + "'";
                if (!string.IsNullOrEmpty(nor.todas))
                    q += " AND Texto:'" + nor.todas + "'";

                string destacado = "&hl.fl=Texto&hl.simple.post=<%2Fspan>&hl.simple.pre=<span%20class%3D%27MatchDestacado%27>&hl=on";


                string url = "select?fl=" + fl + coleccion + bNorma + bDatos + destacado + "&sort=Fecha asc &start=" + nor.pagina;
                url = url.Replace("  ", " ");
                url = url.Replace(",", "%2C").Replace(" ", "%20").Replace(":", "%3A").Replace("'", "%22");
                string fUrl = _configuration["webSolr"] + "/solr/test-1/" + url;
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(fUrl);
                HttpWebResponse response;
                response = (HttpWebResponse)request.GetResponse();

                string responseStr = "";
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    Stream responseStream = response.GetResponseStream();
                    responseStr = new StreamReader(responseStream).ReadToEnd();
                }

                var expConverter = new ExpandoObjectConverter();
                dynamic obj = JsonConvert.DeserializeObject<ExpandoObject>(responseStr, expConverter);

                resp.Code = "OK";
                resp.Message = "Búsqueda exitosa";
                resp.Data = JsonConvert.SerializeObject(obj.response);
                resp.highlighting = JsonConvert.SerializeObject(obj.highlighting);

                return resp;

            }
            catch (Exception ex)
            {
                new TechnicalException("Error metodo Filtro", ex, _configuration);
                resp.Code = "NotFound";
                resp.Message = string.Empty;
                resp.Data = "No es posible buscar por filtro, por favor volver a intentar más tarde.";

                return resp;
            }
        }


        /// <summary>
        /// Busca documentos en Medio Ambiental segun filtro entregado.
        /// </summary>
        /// <param name="nor">Objeto con filtro para la busqueda.</param>
        /// <returns>Lista de aciertos segun criterios entregados.</returns>
        /// <response code="401">No Autorizado. No se ha iniciado sesión.</response>
        /// <response code="400">BadRequest. El protocolo de petición no es el correcto.</response>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [Route("BuscarNM")]
        public ActionResult<Response> BuscarNM(DatosAmbiental nor)
        {
            Response resp = new Response();
            try
            {
                string q = string.Empty;
                string bNorma = string.Empty;
                string bDatos = string.Empty;
                string fecha = string.Empty;
                string fecha2 = string.Empty;
                string coleccion = "&q=Coleccion:'MA'";
                string fl = "Norma,Numero,Articulo,Inciso,Titulo,Fecha,IdDocumento,Organismo,Estado,Partes,Tribunal,Propiedad";

                if (!String.IsNullOrEmpty(nor.ley))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Norma:'LEY'" : "Norma:'LEY'";
                if (!String.IsNullOrEmpty(nor.cir))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Norma:'CIRCULAR'" : "Norma:'CIRCULAR'";
                if (!String.IsNullOrEmpty(nor.dfl))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Norma:'DFL'" : "Norma:'DFL'";
                if (!String.IsNullOrEmpty(nor.con))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Norma:'CONVENIO'" : "Norma:'CONVENIO'";
                if (!String.IsNullOrEmpty(nor.dl))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Norma:'DL'" : "Norma:'DL'";
                if (!String.IsNullOrEmpty(nor.acu))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Norma:'ACUERDO'" : "Norma:'ACUERDO'";
                if (!String.IsNullOrEmpty(nor.ds))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Norma:'DS'" : "Norma:'DS'";
                if (!String.IsNullOrEmpty(nor.tra))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Norma:'TRATADO'" : "Norma:'TRATADO'";
                if (!String.IsNullOrEmpty(nor.dcto))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Norma:'DCTO'" : "Norma:'DCTO'";
                if (!String.IsNullOrEmpty(nor.reg))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Norma:'REGLAMENTO'" : "Norma:'REGLAMENTO'";
                if (!String.IsNullOrEmpty(nor.res))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Norma:'RES'" : "Norma:'RES'";
                if (!String.IsNullOrEmpty(nor.pro))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Norma:'PROTOCOLO'" : "Norma:'PROTOCOLO'";

                if (!String.IsNullOrEmpty(nor.ap))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Tema:'AREAS PROTEGIDAS'" : "Tema:'AREAS PROTEGIDAS'";
                if (!String.IsNullOrEmpty(nor.ap))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Tema:'AREAS PROTEGIDAS'" : "Tema:'AREAS PROTEGIDAS'";
                if (!String.IsNullOrEmpty(nor.agu))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Tema:'AGUA'" : "Tema:'AGUA'";
                if (!String.IsNullOrEmpty(nor.al))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Tema:'AMBIENTE LABORAL'" : "Tema:'AMBIENTE LABORAL'";
                if (!String.IsNullOrEmpty(nor.bio))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Tema:'BIODIVERSIDAD'" : "Tema:'BIODIVERSIDAD'";
                if (!String.IsNullOrEmpty(nor.arq))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Tema:'ARQUEOLOGIA'" : "Tema:'ARQUEOLOGIA'";
                if (!String.IsNullOrEmpty(nor.air))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Tema:'AIRE'" : "Tema:'AIRE'";
                if (!String.IsNullOrEmpty(nor.cde))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Tema:'CLASIFICACION DE ESPECIES'" : "Tema:'CLASIFICACION DE ESPECIES'";
                if (!String.IsNullOrEmpty(nor.com))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Tema:'COMBUSTIBLE'" : "Tema:'COMBUSTIBLE'";
                if (!String.IsNullOrEmpty(nor.adr))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Tema:'AREAS DE RIESGO'" : "Tema:'AREAS DE RIESGO'";
                if (!String.IsNullOrEmpty(nor.ft))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Tema:'FAUNA TERRESTRE'" : "Tema:'FAUNA TERRESTRE'";
                if (!String.IsNullOrEmpty(nor.ind))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Tema:'INDIGENA'" : "Tema:'INDIGENA'";
                if (!String.IsNullOrEmpty(nor.cyp))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Tema:'CLIMA Y PAISAJE'" : "Tema:'CLIMA Y PAISAJE'";
                if (!String.IsNullOrEmpty(nor.ffm))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Tema:'FLORA Y FAUNA MARINA'" : "Tema:'FLORA Y FAUNA MARINA'";
                if (!String.IsNullOrEmpty(nor.inf))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Tema:'INFRAESTRUCTURA'" : "Tema:'INFRAESTRUCTURA'";
                if (!String.IsNullOrEmpty(nor.ene))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Tema:'ENERGIA'" : "Tema:'ENERGIA'";
                if (!String.IsNullOrEmpty(nor.fyv))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Tema:'FLORA Y VEGETACION'" : "Tema:'FLORA Y VEGETACION'";
                if (!String.IsNullOrEmpty(nor.pte))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Tema:'PLANIFICACION TERRITORIAL'" : "Tema:'PLANIFICACION TERRITORIAL'";
                if (!String.IsNullOrEmpty(nor.lum))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Tema:'LUMINICA'" : "Tema:'LUMINICA'";
                if (!String.IsNullOrEmpty(nor.pcu))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Tema:'PATRIMONIO CULTURAL'" : "Tema:'PATRIMONIO CULTURAL'";
                if (!String.IsNullOrEmpty(nor.ryv))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Tema:'RUIDO Y VIBRACIONES'" : "Tema:'RUIDO Y VIBRACIONES'";
                if (!String.IsNullOrEmpty(nor.pla))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Tema:'PLAGUICIDAS'" : "Tema:'PLAGUICIDAS'";
                if (!String.IsNullOrEmpty(nor.sue))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Tema:'SUELO'" : "Tema:'SUELO'";
                if (!String.IsNullOrEmpty(nor.resi))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Tema:'RESIDUOS'" : "Tema:'RESIDUOS'";

                if (!String.IsNullOrEmpty(nor.tribunal))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Organismo:'TRIBUNAL CONSTITUCIONAL'" : "Organismo:'TRIBUNAL CONSTITUCIONAL'";
                if (!String.IsNullOrEmpty(nor.pta))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Organismo:'PRIMER TRIBUNAL AMBIENTAL'" : "Organismo:'PRIMER TRIBUNAL AMBIENTAL'";
                if (!String.IsNullOrEmpty(nor.dict))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Organismo:'CONTRALORIA'" : "Organismo:'CONTRALORIA'";
                if (!String.IsNullOrEmpty(nor.sanit))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Organismo:'SUPERINTENDENCIA DEL MEDIO AMBIENTE'" : "Organismo:'SUPERINTENDENCIA DEL MEDIO AMBIENTE'";
                if (!String.IsNullOrEmpty(nor.csuprema))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Organismo:'CORTE SUPREMA'" : "Organismo:'CORTE SUPREMA'";
                if (!String.IsNullOrEmpty(nor.stp))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Organismo:'SEGUNDO TRIBUNAL AMBIENTAL'" : "Organismo:'SEGUNDO TRIBUNAL AMBIENTAL'";
                if (!String.IsNullOrEmpty(nor.capelaciones))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Organismo:'CORTE DE APELACIONES'" : "Organismo:'CORTE DE APELACIONES'";
                if (!String.IsNullOrEmpty(nor.ttp))
                    q += (!String.IsNullOrEmpty(q)) ? " OR Organismo:'TERCER TRIBUNAL AMBIENTAL'" : "Organismo:'TERCER TRIBUNAL AMBIENTAL'";

                if (!String.IsNullOrEmpty(q))
                    bNorma = " AND (" + q + ")";

                q = string.Empty;

                if (!string.IsNullOrEmpty(nor.FechaD))
                {
                    fecha = nor.FechaD.Replace("/", "-");
                    DateTime fechadoc = Convert.ToDateTime(fecha);
                    fecha = fechadoc.ToString("dd-MM-yyyy");
                    string[] f = fecha.Split('-');
                    fecha = f[2] + "-" + f[1] + "-" + f[0] + @"T00:00:00Z";


                    if (!string.IsNullOrEmpty(nor.FechaH))
                    {
                        fecha2 = nor.FechaH.Replace("/", "-");
                        fechadoc = Convert.ToDateTime(fecha2);
                        fecha2 = fechadoc.ToString("dd-MM-yyyy");
                        f = fecha2.Split('-');
                        fecha2 = f[2] + "-" + f[1] + "-" + f[0] + @"T00:00:00Z";
                        q += " AND Fecha:[" + fecha + " TO " + fecha2 + "]";
                    }
                    else
                    {
                        q += " AND Fecha:'" + fecha + "'";
                    }

                }

                if (!string.IsNullOrEmpty(nor.todas))
                {
                    if (!string.IsNullOrEmpty(nor.ninguna))
                        q += " AND Texto:'" + nor.todas + "' AND NOT Texto:'" + nor.ninguna + "'";
                    else
                        q += " AND Texto:'" + nor.todas + "'";
                }

                if (!string.IsNullOrEmpty(nor.ninguna))
                    q += " AND NOT Texto :'" + nor.ninguna + "'";

                if (!String.IsNullOrEmpty(nor.exacta))
                {
                    q += " AND Texto:'" + nor.exacta + "'";
                }

                if (!string.IsNullOrEmpty(nor.plus))
                    q += " AND Texto:'*" + nor.plus + "*'";

                if (!string.IsNullOrEmpty(nor.num))
                    q += " AND Numero:'" + nor.num + "'";

                if (!string.IsNullOrEmpty(nor.numNorma))
                    q += " AND Numero:'" + nor.numNorma + "'";

                if(!string.IsNullOrEmpty(nor.organismo))
                    q += " AND Organismo:'" + nor.organismo + "'";

                string destacado = "&hl.fl=Texto&hl.simple.post=<%2Fspan>&hl.simple.pre=<span%20class%3D%27MatchDestacado%27>&hl=on";


                string url = "select?fl=" + fl + coleccion + bNorma + bDatos + destacado + "&sort=Fecha asc &start=" + nor.pagina;
                url = url.Replace("  ", " ");
                url = url.Replace(",", "%2C").Replace(" ", "%20").Replace(":", "%3A").Replace("'", "%22");
                string fUrl = _configuration["webSolr"] + "/solr/test-1/" + url;
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(fUrl);
                HttpWebResponse response;
                response = (HttpWebResponse)request.GetResponse();

                string responseStr = "";
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    Stream responseStream = response.GetResponseStream();
                    responseStr = new StreamReader(responseStream).ReadToEnd();
                }

                var expConverter = new ExpandoObjectConverter();
                dynamic obj = JsonConvert.DeserializeObject<ExpandoObject>(responseStr, expConverter);

                resp.Code = "OK";
                resp.Message = "Búsqueda exitosa";
                resp.Data = JsonConvert.SerializeObject(obj.response);
                resp.highlighting = JsonConvert.SerializeObject(obj.highlighting);

                return resp;

            }
            catch (Exception ex)
            {
                new TechnicalException("Error metodo Filtro", ex, _configuration);
                resp.Code = "NotFound";
                resp.Message = string.Empty;
                resp.Data = "No es posible buscar por filtro, por favor volver a intentar más tarde.";

                return resp;
            }
        }


        /// <summary>
        /// Busca imagen del documento recepcionado.
        /// </summary>
        /// <param name="imagen"></param>
        /// <returns>devuelve un Json con la imagen en bit.</returns>
        /// <response code="401">No Autorizado. No se ha iniciado sesión.</response>               
        /// <response code="400">BadRequest. El protocolo de petición no es el correcto.</response>
        [HttpPost]
        [Route("ImagenesDoe")]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<Response> ImagenesDoe(string imagen)
        {
            string respError = string.Empty;
            Response resp = new Response();
            try
            {
                //20180102AEAB1C1AA5356B551760805CE8686A7C
                string a = imagen.Substring(0, 4);
                string m = imagen.Substring(4, 2);
                string d = imagen.Substring(6, 2);
                string i = imagen.Substring(8, imagen.Length - 8);
                string extension = (i.Substring(i.LastIndexOf("_"), i.Length - i.LastIndexOf("_"))).Replace("_", "");
                string directorio_imagenes = _configuration["PATH_IMG:key"];
                var dir = directorio_imagenes + a + "\\" + m + "\\" + d + "\\" + i.Replace("_", ".");
                if (!System.IO.File.Exists(dir))
                    respError = "La imagen no se encuentra en el servidor, por favor verificar";

                resp.Code = HttpStatusCode.OK.ToString();
                resp.Message = string.Empty;
                byte[] imgdata = System.IO.File.ReadAllBytes(dir);

                resp.Data = JsonConvert.SerializeObject(imgdata);
                return resp;
            }
            catch (Exception ex)
            {
                new TechnicalException("Error metodo Buscar imagenes", ex, _configuration);
                resp.Code = HttpStatusCode.NotFound.ToString();
                resp.Message = string.IsNullOrEmpty(respError) ? string.Empty : respError;
                resp.Data = "No es posible realizar la busqueda, por favor volver a intentar más tarde.";
                return resp;
            }
        }

        /// <summary>
        /// Busca la notas del documento recepcionado.
        /// </summary>
        /// <param name="id"></param>
        /// <returns>devuelve un Json con la nota en html.</returns>
        /// <response code="401">No Autorizado. No se ha iniciado sesión.</response>               
        /// <response code="400">BadRequest. El protocolo de petición no es el correcto.</response>
        [HttpPost]
        [Route("Notas")]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<Response> Notas(string id)
        {
            Response resp = new Response();
            try
            {
                string r = _configuration["PATH_NOTAS:key"]; ;

                string txt = System.IO.File.ReadAllText(r + id + ".html", Encoding.UTF8);


                resp.Code = HttpStatusCode.OK.ToString();
                resp.Message = string.Empty;
                resp.Data = JsonConvert.SerializeObject(txt);
                return resp;
            }
            catch (Exception ex)
            {
                new TechnicalException("Error metodo notas", ex, _configuration);
                resp.Code = HttpStatusCode.NotFound.ToString();
                resp.Message = string.Empty;
                resp.Data = "No es posible realizar la busqueda, por favor volver a intentar más tarde.";
                return resp;
            }
        }

        /// <summary>
        /// Busca la version del documento recepcionado.
        /// </summary>
        /// <param name="id"></param>
        /// <returns>devuelve un Json con el documento versionado en html.</returns>
        /// <response code="401">No Autorizado. No se ha iniciado sesión.</response>               
        /// <response code="400">BadRequest. El protocolo de petición no es el correcto.</response>
        [HttpPost]
        [Route("VerVersion")]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<Response> VerVersion(string id)
        {
            Response resp = new Response();
            string directorio_ma = _configuration["PATH_:key"];
            try
            {
                string[] split = id.Split("_");
                string idOri = split[0];
                string versionDoc = split[1];
                string urlSolr = _configuration["webSolr"];
                string url = urlSolr + "/solr/test-1/select?q=id%3A" + idOri + "%20OR%20IdDocumento%3A" + idOri;
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                HttpWebResponse response;
                response = (HttpWebResponse)request.GetResponse();

                string responseStr = "";
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    Stream responseStream = response.GetResponseStream();
                    responseStr = new StreamReader(responseStream).ReadToEnd();
                }

                var expConverter = new ExpandoObjectConverter();
                dynamic obj = JsonConvert.DeserializeObject<ExpandoObject>(responseStr, expConverter);
                string idDocumento = "";
                string id_ = "";
                string Coleccion = "";
                string Origen = "";
                string Fecha = "";
                string xml = "";
                foreach (var doc_ in obj.response.docs)
                {
                    foreach (var v in doc_.Coleccion)
                    {
                        Coleccion = v;
                    }
                    try
                    {
                        Origen = doc_.Origen;
                    }
                    catch { }
                    id_ = doc_.id;
                    idDocumento = doc_.IdDocumento;
                    Fecha = Convert.ToString(doc_.Fecha);
                    //Norma = (doc_.Norma).Replace(" ", "_") + "\\";
                }
                if (Origen == "")
                    Origen = Coleccion;
                else
                    Coleccion = Origen;

                if (Coleccion != "BITE" && Coleccion != "MA" && Coleccion != "LA")
                {
                    DateTime fechadoc = Convert.ToDateTime(Fecha);
                    Fecha = fechadoc.ToString("dd-MM-yyyy");
                    string[] f = Fecha.Split('-');
                    Fecha = f[2] + "\\" + f[1] + "\\" + f[0];
                    Coleccion = "DOE\\" + Fecha;
                }
                string ruta = directorio_ma + Coleccion + "\\" + idDocumento + "_" + versionDoc + ".xml";
                xml = System.IO.File.ReadAllText(ruta);


                resp.Code = "OK";
                resp.Message = "Búsqueda exitosa";
                resp.Data = xml;
                return resp;

            }
            catch (Exception ex)
            {
                new TechnicalException("Error metodo QueryById", ex, _configuration);
                resp.Code = "NotFound";
                resp.Message = string.Empty;
                resp.Data = "No es posible buscar por id de documento, por favor volver a intentar más tarde.";

                return resp;
            }
        }

        private string DecodeHtmlText(string texto)
        {
            StringWriter myWriter = new StringWriter();
            HttpUtility.HtmlDecode(texto, myWriter);
            return myWriter.ToString();
        }
    }
}