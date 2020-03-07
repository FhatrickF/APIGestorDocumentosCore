using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace APIGestorDocumentos.Models
{
    public class SolrXml
    {
        public int IdDocumento { get; set; }
        public string Titulo { get; set; }
        public string Descripcion { get; set; }
        public Nullable<System.DateTime> FechaCreacion { get; set; }
        public int Version { get; set; }
        public bool VersionFinal { get; set; }
        public bool EsBorrador { get; set; }
        public string Texto { get; set; }
    }
}