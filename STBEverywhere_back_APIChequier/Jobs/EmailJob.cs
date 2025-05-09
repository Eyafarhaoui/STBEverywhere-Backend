using Microsoft.EntityFrameworkCore;
using STBEverywhere_back_APIChequier.Services;
using STBEverywhere_Back_SharedModels.Data;

namespace STBEverywhere_back_APIChequier.Jobs
{
    public class EmailJob : BackgroundService
    {


        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<EmailJob> _logger;

        public EmailJob(IServiceProvider serviceProvider, ILogger<EmailJob> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                        var emailService = scope.ServiceProvider.GetRequiredService<EmailService>();

                        var emailsNonEnvoyes = await context.EmailLogs.Where(e => !e.IsEnvoye).ToListAsync();

                        foreach (var email in emailsNonEnvoyes)
                        {
                           /* if (await emailService.SendEmailAsync(email.Destinataire, email.Sujet, email.Contenu))
                            {
                                email.IsEnvoye = true;
                            }*/
                        }

                        await context.SaveChangesAsync();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erreur lors de l'envoi des e-mails.");
                }

                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }
}
