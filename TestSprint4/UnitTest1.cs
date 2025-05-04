using Moq;
using System.Security.Claims;
using Xunit;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using STBEverywhere_back_APIClient.Controllers;
using STBEverywhere_Back_SharedModels;
using STBEverywhere_Back_SharedModels.Models.DTO;
using STBEverywhere_ApiAuth.Repositories;
using STBEverywhere_Back_SharedModels.Data;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Xunit.Abstractions;

namespace TestSprint4
{
    public class UnitTest1
    {
        private readonly DbContextOptions<ApplicationDbContext> _dbContextOptions;
        private readonly ITestOutputHelper _output; // Ajout de l'output

        public UnitTest1(ITestOutputHelper output)
        {
            _output = output; // Initialisation de l'output
            _dbContextOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: "TestDb")
                .Options;
        }

        [Fact]
        public async Task CreateReclamation_ReturnsOkResult_WithValidData()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<ReclamationController>>();
            var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
            var mockUserRepository = new Mock<IUserRepository>();
            var context = new ApplicationDbContext(_dbContextOptions);

            // Créer un faux utilisateur (ClaimsPrincipal) avec un Id = 1
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, "1")
            };
            var identity = new ClaimsIdentity(claims, "TestAuthType");
            var claimsPrincipal = new ClaimsPrincipal(identity);

            var httpContextMock = new DefaultHttpContext();
            httpContextMock.User = claimsPrincipal;
            mockHttpContextAccessor.Setup(_ => _.HttpContext).Returns(httpContextMock);

            var controller = new ReclamationController(
                context,
                mockUserRepository.Object,
                mockHttpContextAccessor.Object,
                mockLogger.Object
            );

            var reclamationDto = new ReclamationDto
            {
                Objet = "Problème de carte",
                Message = "Ma carte ne fonctionne pas",
                Motif = "Carte bloquée"
            };

            // Simuler que GetClientByUserIdAsync retourne un client
            mockUserRepository.Setup(repo => repo.GetClientByUserIdAsync(1))
                .ReturnsAsync(new Client { Id = 1 });

            // Act
            var result = await controller.CreateReclamation(reclamationDto);

            // Assert
            var objectResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(200, objectResult.StatusCode ?? 200);

            var response = Assert.IsType<ReclamationResponseDto>(objectResult.Value);
            _output.WriteLine($"Réclamation créée : Objet = {response.Objet}, Description = {response.Description}, Statut = {response.Statut}, DateCreation = {response.DateCreation}");

            Assert.Equal(reclamationDto.Objet, response.Objet);
            Assert.Equal(reclamationDto.Message, response.Description);
            Assert.Equal("EnCours", response.Statut);
            Assert.NotEqual(default, response.DateCreation);
        }
    }
}
