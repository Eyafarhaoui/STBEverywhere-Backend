using Xunit;
using Moq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text;
using Microsoft.Extensions.Logging;

using STBEverywhere_back_APICompte.Services.IServices;
using STBEverywhere_back_APICompte.Controllers;
using STBEverywhere_Back_SharedModels.Models;
using STBEverywhere_Back_SharedModels;
using STBEverywhere_Back_SharedModels.Models.DTO;
using Microsoft.AspNetCore.Http;
using STBEverywhere_ApiAuth.Repositories;
using System;
using TestSprint2.Tests.Mocks.ClientInfo;
using Xunit.Abstractions;

public class DemandeModificationDecouvertTests
{
    

    //pour afficher dans console le resultat 
    private readonly ITestOutputHelper _output;

    public DemandeModificationDecouvertTests(ITestOutputHelper output)
    {
        _output = output;
    }



    [Fact]
    public async Task DemandeModificationDecouvert_ShouldReturnCreated_WhenValid()
    {
        // Arrange
        var mockCompteService = new Mock<ICompteService>();
        var mockUserRepository = new Mock<IUserRepository>();
        var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        var mockLogger = new Mock<ILogger<DecouvertApiController>>();

        var controller = new DecouvertApiController(
            mockUserRepository.Object,
            mockCompteService.Object,
            mockHttpContextAccessor.Object,
            mockLogger.Object
        );

        var rib = "1234567890";
        var clientId = 1;

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        controller.ControllerContext.HttpContext.Request.Headers["Authorization"] = "Bearer fake_token";

        mockCompteService.Setup(s => s.GetByRIBAsync(rib)).ReturnsAsync(new Compte
        {
            RIB = rib,
            ClientId = clientId
        });

        mockCompteService.Setup(s => s.GetDemandesModificationAsync(rib, StatutDemandeEnum.EnAttente))
            .ReturnsAsync(new List<DemandeModificationDecouvert>());

        mockCompteService.Setup(s => s.CreateDemandeModificationAsync(It.IsAny<DemandeModificationDecouvert>()))
            .Returns(Task.CompletedTask);

        var clientInfo = new Client
        {
            Nom = "Test",
            Prenom = "Client",
            RevenuMensuel = 1000
        };

        var fakeHttpHandler = new FakeHttpMessageHandler(clientInfo);
        var httpClient = new HttpClient(fakeHttpHandler)
        {
            BaseAddress = new Uri("http://localhost:5260")
        };

        controller.SetHttpClient(httpClient);
        controller.SetFakeUserId(clientId.ToString());

        var demandeDto = new DemandeModificationDecouvertDto
        {
            RIBCompte = rib,
            DecouvertDemande = 1500
        };

        // Act
        var result = await controller.DemandeModificationDecouvert(demandeDto);

        // Assert
        var createdResult = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(201, createdResult.StatusCode ?? 201);

        //  Afficher la réponse retournée dans la console
        var value = createdResult.Value;

        var json = System.Text.Json.JsonSerializer.Serialize(
    value,
    new System.Text.Json.JsonSerializerOptions { WriteIndented = true }
);

        _output.WriteLine("---- Contenu retourné par CreatedAtAction ----");
        _output.WriteLine(json);

        using var document = JsonDocument.Parse(json);

        var demandeJson = document.RootElement.GetProperty("demande");
        var clientInfoJson = document.RootElement.GetProperty("clientInfo");

        Assert.Equal(rib, demandeJson.GetProperty("RIBCompte").GetString());
        Assert.Equal(1500, demandeJson.GetProperty("DecouvertDemande").GetInt32());
        Assert.Equal("Test", clientInfoJson.GetProperty("Nom").GetString());
        Assert.Equal("Client", clientInfoJson.GetProperty("Prenom").GetString());
        Assert.Equal(1000, clientInfoJson.GetProperty("RevenuMensuel").GetInt32());

    }


}
