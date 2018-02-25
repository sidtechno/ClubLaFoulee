using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ClubLaFoulee.Models
{
    public class TransferViewModel
    {
        public int ImportedCount { get; set; }

        public string Imported
        {
            get { return string.Format("{0} membres importés", ImportedCount); }
        }
    }
}