using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace APIGestorDocumentosCore.Models
{
    public class Sociedad
    {
        public string DO { get; set; }
        public string RES { get; set; }
        public string Constitucion { get; set; }
        public string Modificacion { get; set; }
        public string Disolucion { get; set; }
        public string FechaD { get; set; }
        public string FechaH { get; set; }
        public string pagina { get; set; }
    }
}
