using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ClubLaFoulee.Models
{
    public class UserViewModel
    {
        public int Id { get; set; }
        public string Firstname { get; set; }
        public string Lastname { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string DateOfBirth { get; set; }

        public string Fullname
        {
            get
            {
                return string.Format("{0} {1}", Firstname, Lastname);
            }
        }
    }
}