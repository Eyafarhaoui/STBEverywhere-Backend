using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using STBEverywhere_ApiAuth.Repositories;
using STBEverywhere_back_APIClient.Controllers;
using STBEverywhere_back_APIClient.Repositories;
using STBEverywhere_back_APIClient.Services;
using STBEverywhere_Back_SharedModels;
using STBEverywhere_Back_SharedModels.Data;
using System.Security.Claims;
using System.Web.Mvc;
using Microsoft.EntityFrameworkCore;

using ControllerContext = Microsoft.AspNetCore.Mvc.ControllerContext;
using Xunit.Abstractions;

namespace TestSprint1
{
    public class UnitTest1
    {




        private readonly ITestOutputHelper _output;

        public UnitTest1(ITestOutputHelper output)
        {
            _output = output;
        }


        [Fact]
        public async Task GetClientInfoFromId_ShouldReturnOk_WhenClientExists()
        {
            // Arrange
            var userId = 42;
            var expectedClient = new Client
            {
                Id = 1,
                Nom = "Tounsi",
                Prenom = "Tounes",
                Genre = "Femme",
                DateNaissance = new DateTime(1980, 1, 1),
                Email = "TounsiTounes@gmail.com",
                Adresse = "Av. de France, Radès.",
                Telephone = "55222555",
                Civilite = "M",
                Nationalite = "Tunisienne",
                EtatCivil = "Célibataire",
                Residence = "Tunis",
                NumCIN = "14668061",
                DateDelivranceCIN = new DateTime(2010, 1, 1),
                DateExpirationCIN = new DateTime(2030, 1, 1),
                LieuDelivranceCIN = "New York",
                PhotoClient = "Tounes.jpg",
                Profession = "Développeur",
                SituationProfessionnelle = "Employé",
                NiveauEducation = "Bac+3",
                NombreEnfants = 2,
                RevenuMensuel = 5000.00m,
                PaysNaissance = "TN",
                NomMere = "EYA Tounsi",
                NomPere = "Mouhamed Tounsi",
                AgenceId = "6801861dfe110f2e59031111",
                id_convention = 1,
                UserId = 1
            };

            var mockUserRepo = new Mock<IUserRepository>();
            mockUserRepo.Setup(r => r.GetClientByUserIdAsync(userId))
                .ReturnsAsync(expectedClient);

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            var dbContext = new ApplicationDbContext(options);

            var controller = new ClientController(
                Mock.Of<IClientService>(),
                mockUserRepo.Object,
                Mock.Of<IHttpContextAccessor>(),
                Mock.Of<IWebHostEnvironment>(),
                dbContext,
                Mock.Of<ILogger<ClientController>>(),
                Mock.Of<INotificationService>(),
                Mock.Of<IClientRepository>()
            );

            // Act
            var result = await controller.GetClientInfoFromId(userId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var client = Assert.IsType<Client>(okResult.Value);
            Assert.Equal(expectedClient.Id, client.Id);
            Assert.Equal(expectedClient.Nom, client.Nom);
            Assert.Equal(expectedClient.UserId, client.UserId);



            _output.WriteLine("Test réussi : les informations du client ont été correctement récupérées depuis le contrôleur.");
            _output.WriteLine($"Client récupéré :");
            _output.WriteLine($"   - Identifiant (Id) : {client.Id}");
            _output.WriteLine($"   - Nom              : {client.Nom}");
            _output.WriteLine($"   - Prenom              : {client.Prenom}");
            _output.WriteLine($"   - Telephone              : {client.Telephone}");

            _output.WriteLine($"   - Identifiant utilisateur (UserId) : {client.UserId}");

        }

    }
}