using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace APIGestorDocumentos.Models
{
    public class Response
    {
        public string Code { get; set; }
        public string Message { get; set; }
        public string Data { get; set; }
        public string highlighting { get; set; }
    }
}