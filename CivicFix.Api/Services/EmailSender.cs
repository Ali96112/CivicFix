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
            var portText = _configuration["Email:SmtpPort"];          // "587" — still text at this point
            var senderEmail = _configuration["Email:SenderEmail"];    // your gmail address
            var senderPassword = _configuration["Email:SenderPassword"]; // your app password
            var senderName = _configuration["Email:SenderName"];      // "CivicFix" — the display name

            // Every value above is null when the "Email" section is missing from
            // appsettings.json, and int.Parse(null) throws ArgumentNullException —
            // a 500 whose message ("Value cannot be null. Parameter 's'") says
            // nothing about the real cause. Check them here instead, so the error
            // names the setting that is actually missing.
            if (string.IsNullOrWhiteSpace(host))
                throw new InvalidOperationException("Email:SmtpHost is missing from appsettings.json.");

            if (string.IsNullOrWhiteSpace(senderEmail))
                throw new InvalidOperationException("Email:SenderEmail is missing from appsettings.json.");

            if (string.IsNullOrWhiteSpace(senderPassword))
                throw new InvalidOperationException(
                    "Email:SenderPassword is not set. Add the Gmail app password to appsettings.json, " +
                    "or set it with: dotnet user-secrets set \"Email:SenderPassword\" \"<app password>\"");

            if (!int.TryParse(portText, out int port))
                throw new InvalidOperationException(
                    $"Email:SmtpPort must be a number. Found: '{portText ?? "(missing)"}'. Gmail uses 587.");

            if (string.IsNullOrWhiteSpace(senderName))
                senderName = "CivicFix";

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