using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace APIGestorDocumentosCore.Models
{
    public class MedioAmbiental
    {
        public string id { get; set; }
        public string IdDocumento { get; set; }
        public string Seccion { get; set; }
        public DateTime Fecha { get; set; }
        public string Numero { get; set; }
        public string Norma { get; set; }
        [Required]
        public string Organismo { get; set; }
        public string SubOrganismo { get; set; }
        public string Categoria { get; set; }
        public string Tema { get; set; }
        [Required]
        public string Titulo { get; set; }
        //[AllowHtml]
        [Required]
        public string Texto { get; set; }
        public bool EsBorrador { get; set; }
        public List<VersionesDocumento> Versiones { get; set; }
    }

    public class VersionesDocumento
    {
        public string id { get; set; }
        public string nombre { get; set; }
    }
}
