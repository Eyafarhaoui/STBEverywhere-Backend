using STBEverywhere_back_APIClient.Repositories;
using STBEverywhere_Back_SharedModels;
using STBEverywhere_Back_SharedModels.Models.DTO;
using STBEverywhere_Back_SharedModels.Models;
using System.Net.Http;
using STBEverywhere_ApiAuth.Repositories;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using STBEverywhere_Back_SharedModels.Data;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using System.Text;

namespace STBEverywhere_back_APIClient.Services
{
    public class ClientService : IClientService
    {
        private readonly IClientRepository _clientRepository;
        private readonly IUserRepository _userRepository;
        private readonly HttpClient _httpClient;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache _memoryCache;
        public ClientService(
            IUserRepository userRepository,
            IClientRepository clientRepository,
            HttpClient httpClient,
            IHttpContextAccessor httpContextAccessor, IMemoryCache memoryCache, ApplicationDbContext context)
        {
            _clientRepository = clientRepository;
            _httpClient = httpClient;
            _userRepository = userRepository;
            _httpContextAccessor = httpContextAccessor;
            _context = context;
            _memoryCache = memoryCache;
        }

        public async Task<string> RegisterAsync(RegisterDto registerDto)
        {
            // Vérifications existantes (RIB, email, etc.)
            var compte = await GetCompteByRIBAsync(registerDto.RIB);
            if (compte == null)
                throw new InvalidOperationException("Le RIB est invalide ou n'existe pas.");

            var client = await _clientRepository.GetClientByIdAsync(compte.ClientId);
            if (client == null || client.Email != registerDto.Email)
                throw new InvalidOperationException("Le RIB ne correspond pas à l'email fourni.");

            // Vérifier si l'utilisateur existe déjà
            var existingUser = await _userRepository.GetByEmailAsync(registerDto.Email);
            if (existingUser != null)
            {
                return "Utilisateur déjà inscrit.";
            }

            // Stocker les infos temporairement dans le cache
            var verificationToken = Guid.NewGuid().ToString();
            _memoryCache.Set(verificationToken, new
            {
                Email = registerDto.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(registerDto.Password),
                Role = UserRole.Client,
                RIB = registerDto.RIB
            }, TimeSpan.FromHours(24));

            // Envoyer l'email avec le lien de vérification
            var verificationUrl = $"http://localhost:4200/verify-email?token={Uri.EscapeDataString(verificationToken)}";

            // Exemple de corps d'e-mail AVEC LIEN DIRECT (évite le tracking Google)
            var emailBody = $@"
<p>Cliquez sur ce lien :</p>
<p>
    <a href='{verificationUrl}' 
       target='_blank' 
       style='color: #0066cc; text-decoration: none;'>
       Vérifier mon e-mail
    </a>
</p>

";
            var emailSubject = "Vérification de votre email";


            // Envoi via l'API email existante
            var emailRequest = new
            {
                to = registerDto.Email,
                subject = emailSubject,
                content = emailBody
            };

            using (var httpClient = new HttpClient())
            {
                var json = JsonSerializer.Serialize(emailRequest);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync("http://localhost:5203/api/Email/send", content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    // _logger.LogError("Erreur lors de l'envoi de l'email de vérification : {Error}", errorContent);
                    throw new Exception("Erreur lors de l'envoi de l'email de vérification.");
                }
            }
            return "Un email de vérification a été envoyé. Veuillez vérifier votre boîte mail pour compléter l'inscription. Ce lien expirera dans 24 heures.";
        }

        public async Task<string> VerifyEmailAsync(string token)
        {
            if (!_memoryCache.TryGetValue(token, out dynamic verificationData))
            {
                throw new UnauthorizedAccessException("Lien de vérification invalide ou expiré.");
            }

            string email = verificationData.Email;
            string passwordHash = verificationData.PasswordHash;
            UserRole role = verificationData.Role;
            string rib = verificationData.RIB;

            // Créer l'utilisateur seulement maintenant
            var newUser = new User
            {
                Email = email,
                PasswordHash = passwordHash,
                Role = role
                // Pas besoin de IsVerified puisque nous ne modifions pas la base
            };
            await _userRepository.AddAsync(newUser);

            // Lier l'utilisateur au client
            var client = await _clientRepository.GetClientByEmailAsync(email);
            if (client != null)
            {
                client.UserId = newUser.Id;
                await _clientRepository.UpdateClientAsync(client);
            }

            // Supprimer le token du cache
            _memoryCache.Remove(token);

            return "Email vérifié avec succès. Votre inscription est maintenant complète.";
        }
      

        public async Task<Convention?> GetConventionByIdAsync(int id)
        {
            return await _context.Conventions
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.id_convention == id);
        }
        public async Task<Client> GetClientByIdAsync(int clientId)
        {
            return await _clientRepository.GetClientByIdAsync(clientId);
        }
        public async Task<bool> UpdateClientInfoAsync(int clientId, Client updatedClient)
        {
            // 1. Récupérer le client existant
            var existingClient = await _clientRepository.GetClientByIdAsync(clientId);
            if (existingClient == null)
                return false;

            // 2. Mapper uniquement les champs autorisés à modifier
            existingClient.Telephone = updatedClient.Telephone;
            existingClient.Email = updatedClient.Email;
            existingClient.Adresse = updatedClient.Adresse;
            existingClient.Civilite = updatedClient.Civilite;
            existingClient.EtatCivil = updatedClient.EtatCivil;
            existingClient.Residence = updatedClient.Residence;
            existingClient.SituationProfessionnelle = updatedClient.SituationProfessionnelle;
            existingClient.NiveauEducation = updatedClient.NiveauEducation;
            existingClient.NombreEnfants = updatedClient.NombreEnfants;
            existingClient.RevenuMensuel = updatedClient.RevenuMensuel;


            // 3. Appliquer les modifications
            await _clientRepository.UpdateClientAsync(existingClient);
            return true;
        }

     
       

        // Méthode pour récupérer le compte par RIB via API externe
        private async Task<Compte?> GetCompteByRIBAsync(string rib)
        {
            var response = await _httpClient.GetAsync($"http://localhost:5185/api/compte/GetByRIB/{rib}");

            if (!response.IsSuccessStatusCode)
                return null;

            var comptes = await response.Content.ReadFromJsonAsync<List<Compte>>();
            return comptes?.FirstOrDefault();
        }
        public async Task<bool> UploadProfileImageAsync(int clientId, string fileName)
        {
            var client = await _context.Clients.FindAsync(clientId);
            if (client == null)
            {
                return false;
            }

            client.PhotoClient = fileName;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> RemoveProfileImageAsync(int clientId)
        {
            var client = await _context.Clients.FindAsync(clientId);
            if (client == null || string.IsNullOrEmpty(client.PhotoClient))
            {
                return false;
            }

            client.PhotoClient = null;
            await _context.SaveChangesAsync();
            return true;
        }




       



    }
}
