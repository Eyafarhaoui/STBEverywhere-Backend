using Xunit;
using Moq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System.Net.Http;
using STBEverywhere_Back_SharedModels.Models;
using STBEverywhere_Back_SharedModels.Models.enums;
using Microsoft.EntityFrameworkCore;

using STBEverywhere_ApiAuth.Repositories;
using STBEverywhere_back_APICarte.Controllers;
using STBEverywhere_back_APICarte.Repository;
using STBEverywhere_back_APICarte.Services;
using STBEverywhere_Back_SharedModels.Data;
using STBEverywhere_Back_SharedModels.Models.DTO;
using STBEverywhere_Back_SharedModels;
using Xunit.Abstractions;

namespace TestSprint3
{
    public class CarteApiControllerTests
    {

        private readonly ITestOutputHelper _output;
        public CarteApiControllerTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task GetCarteDetails_ShouldReturnCarte_WhenCarteExists()
        {
            // Arrange
            var numCarte = "1234567890123456";
            var clientId = 1;

            var mockCarteService = new Mock<ICarteService>();
            var mockCarteRepository = new Mock<ICarteRepository>();
            var mockCvvService = new Mock<ICvvGeneratorService>();
            var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
            var mockUserRepo = new Mock<IUserRepository>();
            var mockHttpClient = new HttpClient(); // Mocking HttpClient is tricky, a real instance can be fine here
            var mockLogger = new Mock<ILogger<CarteService>>();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: "TestDb")
                .Options;

            var dbContext = new ApplicationDbContext(options);

            var context = new DefaultHttpContext();
            context.Request.Headers["Authorization"] = "Bearer fake_token";

            mockHttpContextAccessor.Setup(h => h.HttpContext)
                                   .Returns(context);
            var controller = new CarteController(
       mockCarteService.Object,
       mockCarteRepository.Object,
       mockCvvService.Object,
       mockHttpContextAccessor.Object,
       mockUserRepo.Object,
       mockHttpClient,
       mockLogger.Object,
       dbContext
   );

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = context
            };

            controller.SetFakeUserId(clientId.ToString()); // <-- ligne ajoutée ici

            mockUserRepo.Setup(r => r.GetClientByUserIdAsync(It.IsAny<int>()))
                .ReturnsAsync(new Client { Id = clientId });

            mockCarteService.Setup(s => s.GetCarteDetailsAsync(numCarte))
                .ReturnsAsync(new CarteDetails
                {
                    NumCarte = numCarte,
                    NomCarte = NomCarte.Test,
                    TypeCarte = TypeCarte.National,
                    Statut = StatutCarte.Active
                });

            // Act
            var result = await controller.GetCarteDetails(numCarte);

            // Assert : vérifier que le résultat est de type 200 OK
            var okResult = Assert.IsType<OkObjectResult>(result.Result);

            // Assert : vérifier que la valeur retournée est bien une instance de CarteDetails
            var carte = Assert.IsType<CarteDetails>(okResult.Value);

            // Vérification du contenu de la carte retournée
            Assert.Equal(numCarte, carte.NumCarte);                          // Numéro de carte
            Assert.Equal(NomCarte.Test, carte.NomCarte);                     // Nom de la carte (enum)
            Assert.Equal(TypeCarte.National, carte.TypeCarte);               // Type de la carte (enum)
            Assert.Equal(StatutCarte.Active, carte.Statut);                  // Statut de la carte (enum)

           

            _output.WriteLine($"détails de la carte:");
            _output.WriteLine($"Numéro     : {carte.NumCarte}");
            _output.WriteLine($"Nom        : {carte.NomCarte}");
            _output.WriteLine($"Type       : {carte.TypeCarte}");
            _output.WriteLine($"Statut     : {carte.Statut}");

        }
    }
}
