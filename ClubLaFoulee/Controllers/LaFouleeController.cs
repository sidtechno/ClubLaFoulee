using ClubLaFoulee.Helper;
using ClubLaFoulee.Models;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Mvc;

namespace ClubLaFoulee.Controllers
{
    public class LaFouleeController : Controller
    {
        public const string _connectionString = "Server=MYSQL5018.SmarterASP.NET; database=db_9ad71f_foulee_1; UID=9ad71f_foulee_1; password=doogie01";


        // GET: LaFoulee
        public ActionResult Index()
        {

            try
            {
                string connstring = _connectionString;
                var userList = new List<UserViewModel>();

                using (var conn = new MySqlConnection(connstring))
                {
                    conn.Open();
                    MySqlCommand cmd = conn.CreateCommand();

                    cmd.CommandText = "SELECT ID, (SELECT meta_value from wp_usermeta WHERE meta_key = 'first_name' AND user_id = ID) as first_name, (SELECT meta_value from wp_usermeta WHERE meta_key = 'last_name' AND user_id = ID) as last_name,display_name, user_email, user_url FROM wp_users WHERE ID > 100";

                    using (var sqlreader = cmd.ExecuteReader())
                        while (sqlreader.Read())
                        {
                            userList.Add(new Models.UserViewModel()
                            {
                                Id = Convert.ToInt32(sqlreader["ID"]),
                                Firstname = sqlreader["first_name"].ToString(),
                                Lastname = sqlreader["last_name"].ToString(),
                                Email = sqlreader["user_email"].ToString(),
                                Phone = sqlreader["user_url"].ToString().Replace("http://", "")
                            });
                        } // unnecessary to close the connection
                }     // or the reader with the using-stetement

                return View(userList.Where(p => p.Firstname != string.Empty && p.Lastname != string.Empty));
            }
            catch (Exception ex)
            {
                SendExceptionEmail(ex);
            }


            return View();
        }

        public ActionResult Transfer(bool SendEmail = true)
        {
            var model = new TransferViewModel();

            try
            {

                var SportExpertListing = new List<string>();

                WebRequest wrq = WebRequest.Create("http://onlineregistrations.ca/clublafoulee/dbtools/viewentries.php?event=clublafoulee&orderby=id&sortby=ASC");
                wrq.Credentials = new NetworkCredential("lafoulee", "L8ftY4rJ");
                WebResponse wrp = wrq.GetResponse();
                StreamReader sr = new StreamReader(wrp.GetResponseStream());

                string htmlString = sr.ReadToEnd();
                HtmlAgilityPack.HtmlDocument htmlDocument = new HtmlAgilityPack.HtmlDocument();
                htmlDocument.LoadHtml(htmlString);

                //even
                var nodeList = htmlDocument.DocumentNode.SelectNodes("//tr[@class='even']");
                if (nodeList != null)
                {
                    nodeList.ToList().ForEach(even =>
                    {
                        var firstname = even.ChildNodes[9].InnerText.Replace('�', 'é');
                        var lastname = even.ChildNodes[10].InnerText.Replace('�', 'é');
                        var email = even.ChildNodes[11].InnerText;
                        var phone = even.ChildNodes[12].InnerText;
                        var dob = even.ChildNodes[13].InnerText;

                        var user = new UserViewModel()
                        {
                            Firstname = firstname,
                            Lastname = lastname,
                            Email = email,
                            Phone = phone,
                            DateOfBirth = dob
                        };
                        InsertUser(user, SportExpertListing, SendEmail);
                    });

                    //odd
                    var nodeList2 = htmlDocument.DocumentNode.SelectNodes("//tr[@class='odd']");
                    nodeList2.ToList().ForEach(odd =>
                    {
                        var firstname = odd.ChildNodes[9].InnerText.Replace('�', 'é');
                        var lastname = odd.ChildNodes[10].InnerText.Replace('�', 'é');
                        var email = odd.ChildNodes[11].InnerText;
                        var phone = odd.ChildNodes[12].InnerText;
                        var dob = odd.ChildNodes[13].InnerText;

                        var user = new UserViewModel()
                        {
                            Firstname = firstname,
                            Lastname = lastname,
                            Email = email,
                            Phone = phone,
                            DateOfBirth = dob
                        };
                        InsertUser(user, SportExpertListing, SendEmail);
                    });
                }

                sr.Close();
                wrp.Close();

                //Send Sport Expert Email
                if (SendEmail && SportExpertListing.Count > 0)
                    SendSportExpertEmail(SportExpertListing);

                model.ImportedCount = SportExpertListing.Count();
            }
            catch(Exception ex)
            {
                SendExceptionEmail(ex);
            }

            return View(model);
        }

        private void InsertUser(UserViewModel user, List<string> SportExpertUsers, bool sendEmail)
        {
            string connstring = _connectionString;

            //insert if not exist
            if (!UserExist(user))
            {
                var username = string.Empty;

                #region Insert User
                //insert into wp_user
                using (var conn = new MySqlConnection(connstring))
                {
                    conn.Open();
                    MySqlCommand cmd = conn.CreateCommand();
                    username = BuildUsername(user);

                    cmd.CommandText = string.Format(@"INSERT INTO wp_users(
                          user_login
                          ,user_pass
                          ,user_nicename
                          ,user_email
                          ,user_url
                          ,user_registered
                          ,user_activation_key
                          ,user_status
                          ,display_name
                        ) VALUES (
                          '{0}' 
                          ,MD5('{1}') 
                          ,'{2}' 
                          ,'{3}' 
                          ,'{4}'
                          ,NOW() 
                          ,'' 
                          ,0 
                          ,'{5}'
                        ); select last_insert_id();", username, username, user.Fullname, user.Email, user.Phone, user.Fullname);

                    int userId = Convert.ToInt32(cmd.ExecuteScalar());

                    cmd.CommandText = string.Format(@"INSERT INTO wp_usermeta(
                          user_id
                          , meta_key
                          , meta_value
                        ) VALUES(
                           {0}
                          , 'first_name'
                          , '{1}'
                        )", userId, user.Firstname);
                    cmd.ExecuteScalar();

                    cmd.CommandText = string.Format(@"INSERT INTO wp_usermeta(
                          user_id
                          , meta_key
                          , meta_value
                        ) VALUES(
                           {0}
                          , 'last_name'
                          , '{1}'
                        )", userId, user.Lastname);
                    cmd.ExecuteScalar();

                    SportExpertUsers.Add(user.Fullname);
                }
                #endregion

                #region Send Welcome Email

                if(sendEmail && SportExpertUsers.Count > 0)
                    SendWelcomeEmail(user, username);

                #endregion
            }
        }

        private void SendWelcomeEmail(UserViewModel user, string username)
        {
            var email = new EmailHelper(ConfigurationManager.AppSettings["SmtpHost"], 8889, ConfigurationManager.AppSettings["SmtpUsername"], ConfigurationManager.AppSettings["SmtpPassword"], false);
            email.AddToAddress(user.Email);

            email.FromAddress = new System.Net.Mail.MailAddress(ConfigurationManager.AppSettings["FromEmail"].Trim());

            email.Subject = ConfigurationManager.AppSettings["WelcomeEmailSubject"].Trim();

            //Get HTML Template
            string htmlBodyTemplate = GetHTMLTemplate();

            email.Body = string.Format(htmlBodyTemplate, user.Firstname, username);
            email.SendMail();

        }

        private void SendSportExpertEmail(List<string> sportExpertListing)
        {
            var email = new EmailHelper(ConfigurationManager.AppSettings["SmtpHost"], 8889, ConfigurationManager.AppSettings["SmtpUsername"], ConfigurationManager.AppSettings["SmtpPassword"], false);
            email.AddToAddress(ConfigurationManager.AppSettings["SportExpertEmail"]);

            email.FromAddress = new System.Net.Mail.MailAddress(ConfigurationManager.AppSettings["FromEmail"].Trim());

            email.Subject = ConfigurationManager.AppSettings["SportExpertEmailSubject"].Trim();

            //Get HTML Template
            string htmlBodyTemplate = GetSportExpertTemplate();

            email.Body = string.Format(htmlBodyTemplate, string.Join("<br />", sportExpertListing));
            email.SendMail();
        }

        private void SendExceptionEmail(Exception ex)
        {
            var email = new EmailHelper(ConfigurationManager.AppSettings["SmtpHost"], 8889, ConfigurationManager.AppSettings["SmtpUsername"], ConfigurationManager.AppSettings["SmtpPassword"], false);
            email.AddToAddress(ConfigurationManager.AppSettings["ExceptionEmail"]);

            email.FromAddress = new System.Net.Mail.MailAddress(ConfigurationManager.AppSettings["FromEmail"].Trim());

            email.Subject = "Club La Foulée - Error Report";

            //Get HTML Template
            string htmlBodyTemplate = GetExceptionTemplate();

            email.Body = string.Format(htmlBodyTemplate, ex.ToString());
            email.SendMail();
        }

        private string GetExceptionTemplate()
        {
           return  @"<!DOCTYPE html>
                <body>
                    <p>Error while running club La Foulée transfer</p>
                    <p>{0}</p>
                </body>";
        }

        private string GetSportExpertTemplate()
        {
            return @"<!DOCTYPE html>
                <body>
                    <img src='http://doogie-001-site4.etempurl.com/wp-content/uploads/2017/08/Club-la-Foulee-Logo5.jpg' />
                    <p>Bonjour, </p>
                    <p>Voici la liste des nouveaux membres du Club la Foulée :</p>
                    <p>{0}</p>
                    <p>Merci</p>
                    <p><img src='http://doogie-001-site4.etempurl.com/wp-content/uploads/2017/08/signature.jpg' /></p>
                </body>";
        }

        private string GetHTMLTemplate()
        {
            return @"<!DOCTYPE html>
                <body>
                    <img src='http://doogie-001-site4.etempurl.com/wp-content/uploads/2017/08/Club-la-Foulee-Logo5.jpg' />
                    <h2>CONFIRMATION D'INSCRIPTION</h2>
                    <p>Bonjour {0}, bienvenue dans le Club la Foulée, </p>
                    <p>Ce courriel confirme votre inscription dans le Club la Foulée.</p>
                    <p>Votre inscription est valide du 1er janvier 2018 au 31 décembre 2018.</p>
                    <p>Profitez de l'occasion pour en savoir plus sur les avantages offerts aux membres chez nos commanditaires, Sports Experts C4 du Peps et Teraxion en visitant la section «Avantages aux membres»</p>
                    <p>Vous pouvez accéder à la section réservée « Membres » du site web du club avec les informations suivantes :</p>
                    <p>Code d'accès : {1}<br />Mot de passe : {1}</p>
                    <p>Nous avons hâte de vous voir à nos entraînements!</p>
                    <p>Au plaisir,</p>
                    <p><img src='http://doogie-001-site4.etempurl.com/wp-content/uploads/2017/08/signature.jpg' /></p>
                </body>";
        }

        private string BuildUsername(UserViewModel user)
        {
            var result = string.Format("lf{0}1", GetDate(user.DateOfBirth));
            return result;
        }

        private object GetDate(string dateOfBirth)
        {
            var dateSeparated = dateOfBirth.Split(' ');
            var day = dateSeparated[1].Length == 1 ? string.Format("0{0}", dateSeparated[1]) : dateSeparated[1];
            var month = "";
            var year = dateSeparated[2].Remove(0, 2);

            switch (dateSeparated[0].ToUpper())
            {
                case "JANVIER":
                    month = "01";
                    break;
                case "FEVRIER":
                case "FÉVRIER":
                case "F�VRIER":
                    month = "02";
                    break;
                case "MARS":
                    month = "03";
                    break;
                case "AVRIL":
                    month = "04";
                    break;
                case "MAI":
                    month = "05";
                    break;
                case "JUIN":
                    month = "06";
                    break;
                case "JUILLET":
                    month = "07";
                    break;
                case "AOUT":
                case "AO�T":
                case "AOÛT":
                    month = "08";
                    break;
                case "SEPTEMBRE":
                    month = "09";
                    break;
                case "OCTOBRE":
                    month = "10";
                    break;
                case "NOVEMBRE":
                    month = "11";
                    break;
                case "DECEMBRE":
                case "DÉCEMBRE":
                case "D�CEMBRE":
                    month = "12";
                    break;
            }

            return string.Format("{0}{1}{2}", year, month, day);
        }

        private bool UserExist(UserViewModel user)
        {
            try
            {
                if (user.Firstname == string.Empty || user.Lastname == string.Empty) return true;

                string connstring = _connectionString;
                var userList = new List<UserViewModel>();

                using (var conn = new MySqlConnection(connstring))
                {
                    conn.Open();
                    MySqlCommand cmd = conn.CreateCommand();

                    cmd.CommandText = string.Format("SELECT COUNT(*) FROM wp_users WHERE user_email = '{0}' AND user_url = '{1}'", user.Email, user.Phone);

                    var exist = int.Parse(cmd.ExecuteScalar().ToString());
                    return exist > 0;

                }


            }
            catch (Exception ex)
            {

            }
            return false;
        }

        // GET: LaFoulee/Details/5
        public ActionResult Details(int id)
        {
            return View();
        }

        // GET: LaFoulee/Create
        public ActionResult Create()
        {
            return View();
        }

        // POST: LaFoulee/Create
        [HttpPost]
        public ActionResult Create(FormCollection collection)
        {
            try
            {
                // TODO: Add insert logic here

                return RedirectToAction("Index");
            }
            catch
            {
                return View();
            }
        }

        // GET: LaFoulee/Edit/5
        public ActionResult Edit(int id)
        {
            return View();
        }

        // POST: LaFoulee/Edit/5
        [HttpPost]
        public ActionResult Edit(int id, FormCollection collection)
        {
            try
            {
                // TODO: Add update logic here

                return RedirectToAction("Index");
            }
            catch
            {
                return View();
            }
        }

        // GET: LaFoulee/Delete/5
        public ActionResult Delete(int id)
        {
            return View();
        }

        // POST: LaFoulee/Delete/5
        [HttpPost]
        public ActionResult Delete(int id, FormCollection collection)
        {
            try
            {
                // TODO: Add delete logic here

                return RedirectToAction("Index");
            }
            catch
            {
                return View();
            }
        }
    }
}
