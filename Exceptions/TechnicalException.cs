using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace APIGestorDocumentosCore.Exceptions
{
    [Serializable]
    public class TechnicalException : ApplicationException
    {
        /// <summary>
        /// Construye una instancia en base a un mensaje de error y la una excepción original.
        /// </summary>
        /// <param name="mensaje">El mensaje de error.</param>
        /// <param name="original">La excepción original.</param>
        public TechnicalException(string mensaje, Exception original, IConfiguration _configuration)
            : base(mensaje, original)
        {
            CreateLogFiles Err = new CreateLogFiles(_configuration);
            Err.ErrorLog("ErrorLog", mensaje + " : " + original);
        }

        /// <summary>
        /// Construye una instancia en base a un mensaje de error.
        /// </summary>
        /// <param name="mensaje">El mensaje de error.</param>
        public TechnicalException(string mensaje,IConfiguration _configuration)
            : base(mensaje)
        {
            CreateLogFiles Err = new CreateLogFiles(_configuration);
            Err.ErrorLog("ErrorLog", mensaje);
        }
    }
}
