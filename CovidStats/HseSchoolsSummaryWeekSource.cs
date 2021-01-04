using System;
using System.IO;
using System.Net;

namespace CovidStats
{
    public class HseSchoolsSummaryWeekSource
    {
        

        public HseSchoolsSummaryWeekSource(string pUrl, string pFileName)
        {
            Url = pUrl;
            FileName = pFileName;
        }

        public string Url { get; }
        public string FileName { get; set; }

        public byte[] GetBytes()
        {
            if (!File.Exists(FileName))
            {
                try
                {

                    var requestFichier = WebRequest.Create(Url);
                    using WebResponse response = requestFichier.GetResponse();
                    using MemoryStream ms = new MemoryStream();
                    response.GetResponseStream().CopyTo(ms);
                    File.WriteAllBytes(FileName, ms.ToArray());
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }

            return File.ReadAllBytes(FileName);
        }
    }
}