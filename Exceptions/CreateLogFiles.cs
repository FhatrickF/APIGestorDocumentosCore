using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace APIGestorDocumentosCore.Exceptions
{
    public class CreateLogFiles
    {
        private string sLogFormat;
        private string sErrorTime;
        private string sPath;
        private readonly IConfiguration _configuration;

        public CreateLogFiles(IConfiguration configuration)
        {
            _configuration = configuration;

            //sLogFormat used to create log files format :

            // dd/mm/yyyy hh:mm:ss AM/PM ==> Log Message

            sLogFormat = DateTime.Now.ToShortDateString().ToString() + " " + DateTime.Now.ToLongTimeString().ToString() + " ==> ";

            //this variable used to create log filename format "

            //for example filename : ErrorLogYYYYMMDD

            sErrorTime = DateTime.Now.ToString("yyyyMMdd");
            sPath =  _configuration["PATH_LOG:key"];
        }

        public void ErrorLog(string sName, string sErrMsg)
        {
            try
            {
                using (StreamWriter sw = new StreamWriter(sPath + sName + ".txt", true))
                {
                    sw.WriteLine(sLogFormat + sErrMsg);
                    sw.Flush();
                    sw.Close();
                }
            }
            catch (Exception err)
            {
                int a = 0;
            }

        }
    }
}
