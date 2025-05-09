using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using MimeKit;
using STBEverywhere_back_APIChequier.Repository.IRepositoy;
using STBEverywhere_Back_SharedModels.Data;
using STBEverywhere_Back_SharedModels.Models;

namespace STBEverywhere_back_APIChequier.Services
{
    public class EmailService
    {
        private readonly IEmailLogRepository _emailLogRepository;

        private readonly ApplicationDbContext _context;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IEmailLogRepository emailLogRepository, /*ApplicationDbContext context,*/ ILogger<EmailService> logger)
        {
            //_context = context;
            _emailLogRepository = emailLogRepository;
            _logger = logger;
        }

        private readonly string _smtpServer = "smtp.gmail.com";
        private readonly int _smtpPort = 587;
        private readonly string _smtpUser = "farhaouieya@gmail.com";
        private readonly string _smtpPass = "lyxz bipo hllq gcra";



        public async Task LogEmailAsync(string destinataire, string sujet, string contenu, int id, string type)
        {
            var emailLog = new EmailLog
            {
                Destinataire = destinataire,
                EmailType = type,
                Sujet = sujet,
                Contenu = contenu,
                DemandeId = id,
                DateEnvoi = DateTime.UtcNow,
                IsEnvoye = true, 
            };

            _logger.LogInformation("Log email envoyé pour la demande {id} à l'email {destinataire}.", id, destinataire);

            await _emailLogRepository.AddEmailLogAsync(emailLog);
            await _emailLogRepository.SaveChangesAsync();
        }











        /*  public async Task<bool> SendEmailAsync(string toEmail, string subject, string message)
          {
              try
              {
                  var email = new MimeMessage();
                  email.From.Add(new MailboxAddress("Banque", _smtpUser));
                  email.To.Add(new MailboxAddress("", toEmail));
                  email.Subject = subject;
                  email.Body = new TextPart("html") { Text = message };

                  using var smtp = new SmtpClient();
                  await smtp.ConnectAsync(_smtpServer, _smtpPort, SecureSocketOptions.StartTls);
                  await smtp.AuthenticateAsync(_smtpUser, _smtpPass);
                  await smtp.SendAsync(email);
                  await smtp.DisconnectAsync(true);

                  return true; // Succès
              }
              catch (Exception ex)
              {
                  _logger.LogError(ex, $"Échec de l'envoi de l'e-mail à {toEmail}.");
                  return false; // Échec
              }
          }*/
        /*public async Task<bool> LogEmailAsync(string destinataire, string sujet, string contenu, int id,String type)
        {
            var emailLog = new EmailLog
            {
                Destinataire = destinataire,
                EmailType = type,
                Sujet = sujet,
                Contenu = contenu,
                DemandeId = id,
                DateEnvoi = DateTime.UtcNow,
                IsEnvoye = false,
            };

            _logger.LogInformation("Envoi mail dispo en agence de la demande {id} à l'email {destinataire}.", id, destinataire);
            await _emailLogRepository.AddEmailLogAsync(emailLog);

            //_context.EmailLogs.Add(emailLog);
            await _emailLogRepository.SaveChangesAsync();
            //await _context.SaveChangesAsync();

            // Attendre l'envoi de l'e-mail et vérifier s'il a réussi
            //bool emailSent = await SendEmailAsync(destinataire, sujet, contenu);

            bool emailSent = await SendEmailAsync(destinataire, sujet, contenu);
            if (emailSent)
            {
                emailLog.IsEnvoye = true;
                await _emailLogRepository.SaveChangesAsync();
                //await _context.SaveChangesAsync(); // Mettre à jour la base de données
            }

            return emailSent;
        }*/




    }

}

