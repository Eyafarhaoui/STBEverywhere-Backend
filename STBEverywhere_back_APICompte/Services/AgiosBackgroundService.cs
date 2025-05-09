/*namespace STBEverywhere_back_APICompte.Services
{
   public class AgiosBackgroundService : BackgroundService
    {

        private readonly IServiceProvider _services;
        private readonly ILogger<AgiosBackgroundService> _logger;
        private Timer _timer;

        public AgiosBackgroundService(
            IServiceProvider services,
            ILogger<AgiosBackgroundService> logger)
        {
            _services = services;
            _logger = logger;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Service de gestion des agios démarré");
            ScheduleNextRun();
            return Task.CompletedTask;
        }


        private void ScheduleNextRun()
        {
            var now = DateTime.Now;
            //nextRun est calculé une première fois au démarrage du service.(ExecuteAsync)
            //Après chaque exécution, nextRun est recalculé pour le mois suivant.
            //Le système attend(delay) jusqu'à la prochaine exécution.
            //par exemple aujoutd'hui le 7 avril c'est la premiere fois ou je run mon projet
            //.du coup nextrun aura comme valuer 1 mais. et le 1 mais elle aura la valeur
            //delay = (1er mai 2025 à 03h) - (7 avril 2025 à 14h) ≈ 23 jours et 13 heures
            //Attente pendant 23 jours et 13 heures... puis exécution du traitement

            var nextRun = new DateTime(now.Year, now.Month, 1)
                .AddMonths(1)
                .AddHours(3); // 3h du matin le 1er du mois

            var delay = nextRun - now;

            _timer = new Timer(async _ =>
            {
                await ProcessAgios();
                ScheduleNextRun();
            }, null, delay, Timeout.InfiniteTimeSpan);

            _logger.LogInformation($"Prochain calcul des agios programmé pour {nextRun}");
        }




        private async Task ProcessAgios()
        {
            try
            {
                using var scope = _services.CreateScope();
                var agiosService = scope.ServiceProvider.GetRequiredService<AgiosService>();
                await agiosService.CalculerEtAppliquerAgiosMensuels();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur dans le traitement des agios");
            }
        }



        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            _timer?.Dispose();
            await base.StopAsync(stoppingToken);
        }


    }
}
    */