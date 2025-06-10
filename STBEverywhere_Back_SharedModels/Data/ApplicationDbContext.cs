using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using MongoDB.Driver;
using SharpCompress.Common;
using STBEverywhere_Back_SharedModels.Models;
using STBEverywhere_Back_SharedModels.Models.enums;

namespace STBEverywhere_Back_SharedModels.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        // DbSets
        public DbSet<User> Users { get; set; }
        public DbSet<Agent> Agents { get; set; }
        public DbSet<Client> Clients { get; set; }
        public DbSet<Compte> Comptes { get; set; }
        public DbSet<Virement> Virements { get; set; }
        public DbSet<Carte> Cartes { get; set; }
        public DbSet<DemandeCarte> DemandesCarte { get; set; }
        public DbSet<DemandeChequier> DemandesChequiers { get; set; }
        public DbSet<Chequier> Chequiers { get; set; }
        public DbSet<FeuilleChequier> FeuillesChequiers { get; set; }
        public DbSet<FraisChequier> FraisChequiers { get; set; }
        public DbSet<EmailLog> EmailLogs { get; set; }
        public DbSet<Beneficiaire> Beneficiaires { get; set; }

        public DbSet<PackStudent> PackStudents { get; set; }
        public DbSet<PackElyssa> PackElyssa { get; set; }

        public DbSet<FraisCompte> FraisComptes { get; set; }
        public DbSet<PeriodeDecouvert> PeriodeDecouverts { get; set; }
        public DbSet<DemandeModificationDecouvert> DemandeModificationDecouverts { get; set; }
        public DbSet<FraisCarte> FraisCartes { get; set; }
        public DbSet<DemandeAugmentationPlafond> DemandesAugmentationPlafond { get; set; }
        public DbSet<RechargeCarte> RechargesCarte { get; set; }
        public DbSet<Reclamation> Reclamations { get; set; }
        public DbSet<NotificationPack> NotificationsPack { get; set; }
        public DbSet<NotificationReclamation> NotificationsReclamation { get; set; }
        public DbSet<HistoriqueSolde> HistoriquesSoldes { get; set; }
        public DbSet<Convention> Conventions { get; set; }
        public DbSet<ModificationRequest> ModificationRequests { get; set; }
        public DbSet<InteretJournalier> InteretsJournaliers { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configuration de l'entité User
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(u => u.Id);
                entity.Property(u => u.Email).IsRequired().HasMaxLength(100);
                entity.Property(u => u.PasswordHash).IsRequired();
                entity.Property(u => u.Role).IsRequired().HasConversion<string>();
                entity.Property(u => u.IsActive).HasDefaultValue(true);
                entity.Property(u => u.ResetPasswordToken).HasMaxLength(255);
                entity.Property(u => u.ResetPasswordTokenExpiry);

                // Données initiales
                entity.HasData(
                    new User { Id = 1, Email = "farhaouieya@gmail.com", PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"), Role = UserRole.Client },
                    new User { Id = 2, Email = "ikramguesmi75@gmail.com", PasswordHash = BCrypt.Net.BCrypt.HashPassword("password456"), Role = UserRole.Client },
                    new User { Id = 4, Email = "mouradfarhaoui1967@gmail.com", PasswordHash = BCrypt.Net.BCrypt.HashPassword("password789"), Role = UserRole.Client },

                    new User { Id = 3, Email = "agent@stb.com", PasswordHash = BCrypt.Net.BCrypt.HashPassword("agent123"), Role = UserRole.Agent },
                    new User { Id = 5, Email = "agent5@stb.com", PasswordHash = BCrypt.Net.BCrypt.HashPassword("agent456"), Role = UserRole.Agent }

                );
            });

            modelBuilder.Entity<Reclamation>()
           .HasOne(r => r.Client)
           .WithMany(c => c.Reclamations)
           .HasForeignKey(r => r.ClientId);
            modelBuilder.Entity<Reclamation>() // convertir enum en string 
              .Property(r => r.Statut)
              .HasConversion<string>();

            modelBuilder.Entity<NotificationPack>(entity =>
            {
                entity.HasKey(n => n.Id);

                entity.Property(n => n.Title)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(n => n.Message)
                    .IsRequired()
                    .HasMaxLength(500);

                entity.Property(n => n.NotificationType)
                    .IsRequired()
                    .HasMaxLength(50);

                entity.Property(n => n.CreatedAt)
                    .IsRequired();

                entity.HasOne(n => n.Client)
                    .WithMany(c => c.NotificationsPack)
                    .HasForeignKey(n => n.ClientId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<ModificationRequest>(entity =>
            {
                entity.HasKey(m => m.Id);

                entity.Property(m => m.ClientId)
                    .IsRequired();

                entity.Property(m => m.FieldToModify)
                    .IsRequired()
                    .HasMaxLength(50);

                entity.Property(m => m.NewValue)
                    .IsRequired()
                    .HasMaxLength(255);

                entity.Property(m => m.JustificationPath)
                    .IsRequired()
                    .HasMaxLength(255);

                entity.Property(m => m.RequestDate)
                    .IsRequired()
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(m => m.Status)
                    .IsRequired()
                    .HasMaxLength(20)
                    .HasDefaultValue("Pending");

                entity.Property(m => m.ProcessedByAgentId)
                    .IsRequired(false);

                entity.Property(m => m.ProcessedDate)
                    .IsRequired(false);

                // Relation with Client
                entity.HasOne(m => m.Client)
                    .WithMany() // If Client doesn't have a collection of ModificationRequests
                    .HasForeignKey(m => m.ClientId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Add indexes for performance
                entity.HasIndex(m => m.Status);
                entity.HasIndex(m => m.ClientId);
                entity.HasIndex(m => m.RequestDate);
            });
            modelBuilder.Entity<NotificationReclamation>(entity =>
            {
                entity.HasKey(n => n.Id);

                entity.Property(n => n.Title)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(n => n.Message)
                    .IsRequired()
                    .HasMaxLength(500);

                entity.Property(n => n.NotificationType)
                    .IsRequired()
                    .HasMaxLength(50);

                entity.Property(n => n.CreatedAt)
                    .IsRequired();

                entity.HasOne(n => n.Client)
                    .WithMany(c => c.NotificationsReclamation)
                    .HasForeignKey(n => n.ClientId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<FraisCarte>(entity =>
            {
                entity.HasKey(f => f.Id);
                entity.Property(f => f.Montant).HasColumnType("decimal(18,2)");

                // Relation avec Carte
                entity.HasOne(f => f.Carte)
                      .WithMany(c => c.FraisCartes)
                      .HasForeignKey(f => f.NumCarte)
                      .OnDelete(DeleteBehavior.Cascade); // Supprime les frais si la carte est supprimée
            });
            modelBuilder.Entity<DemandeAugmentationPlafond>()
               .HasOne(d => d.Carte)
               .WithMany()
               .HasForeignKey(d => d.NumCarte);

            // Configuration de l'entité Agent
            modelBuilder.Entity<Agent>(entity =>
            {
                entity.HasKey(a => a.Id);
                entity.Property(a => a.Nom).IsRequired().HasMaxLength(50);
                entity.Property(a => a.Prenom).IsRequired().HasMaxLength(50);
                entity.Property(a => a.Departement).HasMaxLength(100);

                // Remove the IsRequired(false) since the property is now nullable by type
                // entity.Property(a => a.UserId).IsRequired(false); // No longer needed

                // Relation with User
                entity.HasOne(a => a.User)
                    .WithOne()
                    .HasForeignKey<Agent>(a => a.UserId)
                    .OnDelete(DeleteBehavior.Restrict);


                entity.HasData(
                        new Agent { Id = 1, Nom = "Admin", Prenom = "STB", Departement = "Administration", UserId = 3, AgenceId = "6801861dfe110f2e59031111" }
                    );
                entity.HasData(
                       new Agent { Id = 2, Nom = "Admin5", Prenom = "STB5", Departement = "Administration", UserId = 5, AgenceId = "6801861dfe110f2e59031112" }
                   );
            });

            // Configuration de l'entité Client
            modelBuilder.Entity<Client>(entity =>
            {
                entity.HasKey(c => c.Id);
                entity.Property(c => c.Nom).IsRequired().HasMaxLength(50);
                entity.Property(c => c.Prenom).IsRequired().HasMaxLength(50);
                entity.Property(c => c.Email).IsRequired().HasMaxLength(100);
                entity.Property(c => c.Telephone).IsRequired().HasMaxLength(20);
                entity.Property(c => c.Adresse).IsRequired().HasMaxLength(200);
                entity.Property(c => c.UserId).IsRequired(false);

                // Relation avec User (sans navigation inverse)
                entity.HasOne(c => c.User)
                    .WithOne()
                    .HasForeignKey<Client>(c => c.UserId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .IsRequired(false);

                // Relations One-to-Many
                entity.HasMany(c => c.Comptes)
                    .WithOne(c => c.Client)
                    .HasForeignKey(c => c.ClientId)
                    .OnDelete(DeleteBehavior.Cascade);


                // Données initiales
                entity.HasData(
                  new Client
                  {
                      Id = 1,
                      Nom = "Guesmi",
                      Prenom = "Mahmoud",
                      DateNaissance = new DateTime(1980, 1, 1),
                      Telephone = "94626064",
                      Email = "farhaouieya@gmail.com", // Vous modifierez cette valeur
                      Adresse = "123 Rue Tunis",
                      Civilite = "M",
                      Nationalite = "TN",
                      EtatCivil = "Marié(e)",
                      Residence = "Tunis",
                      NumCIN = "14668061",
                      DateDelivranceCIN = new DateTime(2010, 1, 1),
                      DateExpirationCIN = new DateTime(2030, 1, 1),
                      LieuDelivranceCIN = "Tunis",
                      PhotoClient = "image1.jpg",
                      Genre = "Masculin",
                      Profession = "Ingénieur",
                      SituationProfessionnelle = "Employé",
                      NiveauEducation = "Master",
                      NombreEnfants = 2,
                      RevenuMensuel = 5000.00m,
                      PaysNaissance = "Tunisie",
                      NomMere = "Mère Mahmoud",
                      NomPere = "Père Mahmoud",
                      AgenceId = "6801861dfe110f2e59031111",
                      id_convention = 1,
                      UserId = 1
                  },
                   new Client
                   {
                       Id = 2, // Nouvel ID
                       Nom = "Guesmi",
                       Prenom = "Ikram",
                       DateNaissance = new DateTime(1990, 5, 15),
                       Telephone = "987654321",
                       Email = "ikramguesmi75@gmail.com", // Vous modifierez cette valeur
                       Adresse = "456 Avenue Habib Bourguiba",
                       Civilite = "Mme",
                       Nationalite = "TN",
                       EtatCivil = "Célibataire",
                       Residence = "Sfax",
                       NumCIN = "14668063",
                       DateDelivranceCIN = new DateTime(2015, 5, 15),
                       DateExpirationCIN = new DateTime(2035, 5, 15),
                       LieuDelivranceCIN = "Sfax",
                       PhotoClient = "image3.jpg",
                       Genre = "Féminin",
                       Profession = "Médecin",
                       SituationProfessionnelle = "Indépendant",
                       NiveauEducation = "Doctorat",
                       NombreEnfants = 0,
                       RevenuMensuel = 7000.00m,
                       PaysNaissance = "Tunisie",
                       NomMere = "Mère Ikram",
                       NomPere = "Père Ikram",
                       AgenceId = "6801861dfe110f2e59031111",
                       id_convention = 2,
                       UserId = 2
                   },
                    new Client
                    {
                        Id = 3, // Nouvel ID
                        Nom = "Jemai",
                        Prenom = "Hayet",
                        DateNaissance = new DateTime(1985, 8, 20),
                        Telephone = "987654322",
                        Email = "guesmii.ikram@gmail.com", // Vous modifierez cette valeur
                        Adresse = "789 Rue de la Liberté",
                        Civilite = "Mme",
                        Nationalite = "TN",
                        EtatCivil = "Marié(e)",
                        Residence = "Sousse",
                        NumCIN = "14668065",
                        DateDelivranceCIN = new DateTime(2016, 8, 20),
                        DateExpirationCIN = new DateTime(2036, 8, 20),
                        LieuDelivranceCIN = "Sousse",
                        PhotoClient = "image2.jpg",
                        Genre = "Féminin",
                        Profession = "Avocate",
                        SituationProfessionnelle = "Profession libérale",
                        NiveauEducation = "Master",
                        NombreEnfants = 3,
                        RevenuMensuel = 6000.00m,
                        PaysNaissance = "Tunisie",
                        NomMere = "Mère Hayet",
                        NomPere = "Père Hayet",
                        AgenceId = "6801861dfe110f2e59031112",
                        id_convention = 3,

                    },

                    new Client
                    {
                        Id = 4,
                        Nom = "Farhaoui",
                        Prenom = "Eya",
                        DateNaissance = new DateTime(1980, 1, 1),
                        Telephone = "55292557",
                        Email = "mouradfarhaoui1967@gmail.com",
                        Adresse = "123 Rue Tunis",
                        Civilite = "M",
                        Nationalite = "TN",
                        EtatCivil = "Marié(e)",
                        Residence = "Tunis",
                        NumCIN = "14668061",
                        DateDelivranceCIN = new DateTime(2010, 1, 1),
                        DateExpirationCIN = new DateTime(2030, 1, 1),
                        LieuDelivranceCIN = "Tunis",
                        PhotoClient = "mahmoud.jpg",
                        Genre = "Masculin",
                        Profession = "Ingénieur",
                        SituationProfessionnelle = "Employé",
                        NiveauEducation = "Master",
                        NombreEnfants = 2,
                        RevenuMensuel = 5000.00m,
                        PaysNaissance = "Tunisie",
                        NomMere = "Mère Mahmoud",
                        NomPere = "Père Mahmoud",
                        AgenceId = "6801861dfe110f2e59031111",
                        id_convention = 1,
                        UserId = 4
                    }
                );
            });

            // Configuration de l'entité Compte
            modelBuilder.Entity<Compte>(entity =>
            {
                entity.HasKey(c => c.RIB);
                entity.Property(c => c.Type).IsRequired().HasMaxLength(50);
                entity.Property(c => c.Solde).HasColumnType("decimal(18,3)");
                entity.Property(c => c.Statut).HasMaxLength(20);


                entity.HasData(
                    new Compte
                    {
                        RIB = "10000001121041340847",
                        NumCin = "14668061",
                        Type = "Courant",
                        Solde = 1000.50m,
                        DateCreation = new DateTime(2024, 5, 1),
                        Statut = "Actif",
                        IBAN = "TN7410000001121041347",
                        DecouvertAutorise = 0,
                        ClientId = 1
                    },
                     new Compte
                     {
                         RIB = "10002960033351809624",
                         NumCin = "14668061",
                         Type = "eparge",
                         Solde = 5000.00m,
                         DateCreation = new DateTime(2025, 6, 1),

                         Statut = "Actif",
                         IBAN = "TN4710002960033351824",
                         ClientId = 4
                     },
                    new Compte
                    {
                        RIB = "10002840658225593444",
                        NumCin = "14668062",
                        Type = "Courant",
                        Solde = 5000.00m,
                        DateCreation = new DateTime(2025, 1, 1),
                        Statut = "Actif",
                        IBAN = "TN2010002840658225544",
                        ClientId = 2
                    }
                );
            });

            // Configuration de l'entité Carte
            modelBuilder.Entity<Carte>(entity =>
            {
                entity.HasKey(c => c.NumCarte);
                entity.Property(c => c.NomCarte).HasConversion<string>();
                entity.Property(c => c.TypeCarte).HasConversion<string>();
                entity.Property(c => c.Statut).HasConversion<string>();

                // Relation avec Compte
                entity.HasOne(c => c.Compte)
                    .WithMany()
                    .HasForeignKey(c => c.RIB)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasData(
                    new Carte
                    {
                        NumCarte = "4314052233334444",
                        NomCarte = NomCarte.VisaClassic,
                        TypeCarte = TypeCarte.International,
                        DateCreation = new DateTime(2024, 1, 1),
                        DateExpiration = new DateTime(2027, 1, 1),
                        Statut = StatutCarte.Active,
                        Iddemande = 1,
                        Nature = "postpayee",
                        PlafondTPE = 4000,
                        PlafondDAP = 2000,
                        CodePIN = "",
                        RIB = "10000001121041340847"
                    },
                    new Carte
                    {
                        NumCarte = "5189326677778888",
                        NomCarte = NomCarte.Mastercard,
                        TypeCarte = TypeCarte.National,
                        DateCreation = new DateTime(2024, 1, 1),
                        DateExpiration = new DateTime(2027, 1, 1),
                        Statut = StatutCarte.Active,
                        Iddemande = 2,


                        Nature = "postpayee",
                        CodePIN = "",
                        PlafondTPE = 4000,
                        PlafondDAP = 2000,
                        RIB = "10002840658225593444"
                    }
                );
            });

            // Configuration de l'entité DemandeCarte
            modelBuilder.Entity<DemandeCarte>(entity =>
            {
                entity.HasKey(d => d.Iddemande);
                entity.Property(d => d.NumCompte).IsRequired().HasMaxLength(20);
                entity.Property(d => d.NomCarte).HasConversion<string>();
                entity.Property(d => d.TypeCarte).HasConversion<string>();
                entity.Property(d => d.Statut).HasConversion<string>();
                entity.Property(d => d.CIN).IsRequired().HasMaxLength(20);
                entity.Property(d => d.Email).IsRequired().HasMaxLength(100);
                entity.Property(d => d.NumTel).IsRequired().HasMaxLength(20);

                entity.HasData(
                                    new DemandeCarte
                                    {
                                        Iddemande = 1,
                                        NumCompte = "10000001121041340847",
                                        NomCarte = NomCarte.VisaClassic,
                                        TypeCarte = TypeCarte.International,
                                        CIN = "14668061",
                                        Email = "guesmiimahmoud@gmail.com",
                                        NumTel = "12345678",
                                        Statut = StatutDemande.DisponibleEnAgence,
                                        EmailEnvoye = false,
                                        EmailEnvoyeLivree = false,
                                        CarteAjouter = true,

                                    },
                                    new DemandeCarte
                                    {
                                        Iddemande = 2,
                                        NumCompte = "10002840658225593444",
                                        NomCarte = NomCarte.Mastercard,
                                        TypeCarte = TypeCarte.National,
                                        CIN = "14668062",
                                        Email = "ikramguesmi75@gmail.com",
                                        NumTel = "87654321",
                                        Statut = StatutDemande.EnPreparation,
                                        EmailEnvoye = false,
                                        EmailEnvoyeLivree = false,
                                        CarteAjouter = true,

                                    }
                                );
            });
            modelBuilder.Entity<Convention>(entity =>
            {
                entity.HasKey(c => c.id_convention); // Clé primaire

                entity.Property(c => c.nom_convention)
                      .IsRequired()
                      .HasMaxLength(100); // Taille max, tu peux ajuster

                entity.Property(c => c.marge_bancaire)
                      .IsRequired()
                      .HasColumnType("decimal(5,2)"); // Format 99.99 (exemple : 1.50, 2.00)

                // Seed data
                entity.HasData(
                    new Convention
                    {
                        id_convention = 1,
                        nom_convention = "CNSS",
                        marge_bancaire = 1.5m
                    },
                    new Convention
                    {
                        id_convention = 2,
                        nom_convention = "CNRPS",
                        marge_bancaire = 2.0m
                    },

                     new Convention
                     {
                         id_convention = 3,
                         nom_convention = "CNAM",
                         marge_bancaire = 3.0m
                     },
                     new Convention
                     {
                         id_convention = 4,
                         nom_convention = "MUTUELLE STEG",
                         marge_bancaire = 3.5m
                     },
                      new Convention
                      {
                          id_convention = 5,
                          nom_convention = "SYNDICAT TUNISIEN MEDECINS DENTISTE DE LIBRE PRATIQUES",
                          marge_bancaire = 4.0m
                      },

                       new Convention
                       {
                           id_convention = 6,
                           nom_convention = "OFFICE NATIONAL DES POSTES TUNISIENNES",
                           marge_bancaire = 3.5m
                       },
                         new Convention
                         {
                             id_convention = 7,
                             nom_convention = "AMICALE MINISTERE ENSEIGNEMENT SUPERIEUR ET RECHERCHE SCIENTIFI",
                             marge_bancaire = 6.0m
                         },
                           new Convention
                           {
                               id_convention = 8,
                               nom_convention = "MUTUELLE STEG",
                               marge_bancaire = 1.0m
                           }
                );
            });
            // Configuration de l'entité Virement
            modelBuilder.Entity<Virement>(entity =>
            {
                entity.HasKey(v => v.Id);
                entity.HasIndex(v => new { v.RIB_Emetteur, v.DateVirement }).IsUnique();
            });

            modelBuilder.Entity<DemandeChequier>()
        .Property(d => d.ModeLivraison)
        .HasConversion<string>();

            modelBuilder.Entity<DemandeChequier>()
                .Property(d => d.Status)
                .HasConversion<string>();



            modelBuilder.Entity<DemandeChequier>()
       .Property(d => d.ModeLivraison)
       .HasConversion<string>();

            // le convertisseur de la liste des idvirement de l'entité FraisCompte
            modelBuilder.Entity<FraisCompte>()
                    .Property(e => e.IdsVirementsStr)
                    .HasDefaultValue(""); // Valeur par défaut vide

            // pour stocker l'enum de statut demande en texte pas 0 1 
            modelBuilder.Entity<DemandeModificationDecouvert>()
       .Property(d => d.StatutDemande)
       .HasConversion<string>();

        }


    }
}
