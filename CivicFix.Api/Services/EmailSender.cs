using System.Net;              // gives us NetworkCredential (login info for the mail server)
using System.Net.Mail;         // gives us MailMessage and SmtpClient (the email tools)

namespace CivicFix.Api.Services  // the folder/namespace this class lives in
{
    public class EmailSender     // our helper class for sending emails
    {
        private readonly IConfiguration _configuration;  // holds access to appsettings.json

        // constructor — .NET hands us the configuration when it creates this class
        public EmailSender(IConfiguration configuration)
        {
            _configuration = configuration;   // save it so other methods can read appsettings
        }

        // the method that actually sends an email — takes recipient, subject, and body
        public async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            var host = _configuration["Email:SmtpHost"];              // "smtp.gmail.com" from appsettings
            var port = int.Parse(_configuration["Email:SmtpPort"]);   // 587 — convert text to number
            var senderEmail = _configuration["Email:SenderEmail"];    // your gmail address
            var senderPassword = _configuration["Email:SenderPassword"]; // your app password
            var senderName = _configuration["Email:SenderName"];      // "CivicFix" — the display name

            var message = new MailMessage();                          // create an empty email
            message.From = new MailAddress(senderEmail, senderName);  // set who it's FROM (your gmail, shown as "CivicFix")
            message.To.Add(toEmail);                                  // set who it's going TO (the user)
            message.Subject = subject;                                // the email subject line
            message.Body = body;                                      // the email content
            message.IsBodyHtml = true;                                // allow HTML so we can add a clickable link

            using (var client = new SmtpClient(host, port))           // open a connection to gmail's mail server
            {
                client.Credentials = new NetworkCredential(senderEmail, senderPassword); // log in with your gmail + app password
                client.EnableSsl = true;                              // gmail requires an encrypted connection

                await client.SendMailAsync(message);                 // actually send the email, wait until done
            }                                                          // "using" auto-closes the connection when finished
        }
    }
}