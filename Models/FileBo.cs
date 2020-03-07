using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using System.Xml;
using System.Xml.Serialization;

namespace APIGestorDocumentos.Models
{
    public class FileBo
    {
        public static byte[] getBytesFromFile(string fullFilePath)
        {
            FileStream fs = File.OpenRead(fullFilePath);
            try
            {
                byte[] bytes = new byte[fs.Length];
                fs.Read(bytes, 0, Convert.ToInt32(fs.Length));
                fs.Close();
                return bytes;
            }
            finally
            {
                fs.Close();
            }
        }

        public static bool setBytesToFile(string fullFilePath, byte[] byteArrayIn)
        {
            File.WriteAllBytes(fullFilePath, byteArrayIn);
            return true;
        }

        public static bool setXmlStringToFile(string fullFilePath, String xml)
        {
            FileStream stream = new FileStream(fullFilePath, FileMode.Create, FileAccess.Write);
            StreamWriter writer = new StreamWriter(stream);
            writer.WriteLine(xml);
            writer.Close();
            return true;
        }

        public static bool setXmlToFile(string fullFilePath, XmlDocument xml)
        {
            StringWriter sw = new StringWriter();
            XmlTextWriter tx = new XmlTextWriter(sw);
            xml.WriteTo(tx);
            String sxml = sw.ToString();
            FileStream stream = new FileStream(fullFilePath, FileMode.Create, FileAccess.Write);
            StreamWriter writer = new StreamWriter(stream);
            writer.WriteLine(sxml);
            writer.Close();

            return true;
        }

        public static string SerializeXML(object objectToSerialize)
        {
            MemoryStream mem = new MemoryStream();
            XmlSerializer ser = new XmlSerializer(objectToSerialize.GetType());
            ser.Serialize(mem, objectToSerialize);
            UTF8Encoding utf8 = new UTF8Encoding();
            return utf8.GetString(mem.ToArray());
        }

        public static string SerializeOverriderXML(object objectToSerialize, XmlAttributeOverrides xOver)
        {
            MemoryStream mem = new MemoryStream();
            XmlSerializer ser = new XmlSerializer(objectToSerialize.GetType(), xOver);
            ser.Serialize(mem, objectToSerialize);
            UTF8Encoding utf8 = new UTF8Encoding();
            return utf8.GetString(mem.ToArray());
        }

        public static object DeserializeXML(Type typeToDeserialize, string xmlString)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(xmlString);
            MemoryStream mem = new MemoryStream(bytes);
            XmlSerializer ser = new XmlSerializer(typeToDeserialize);
            return ser.Deserialize(mem);
        }
    }
}