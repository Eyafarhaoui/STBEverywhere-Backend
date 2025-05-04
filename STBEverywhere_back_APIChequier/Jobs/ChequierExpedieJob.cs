using STBEverywhere_back_APIChequier.Services;

namespace STBEverywhere_back_APIChequier.Jobs
{
    public class ChequierExpedieJob : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ChequierExpedieJob> _logger;

        public ChequierExpedieJob(IServiceProvider serviceProvider, ILogger<ChequierExpedieJob> logger)
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
                        var chequierService = scope.ServiceProvider.GetRequiredService<ChequierService>(); 

                        await chequierService.VérifierChéquiersExpedieAsync();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erreur lors de la vérification des chéquiers expédiés.");
                }

                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); 
            }
        }
    }

}
