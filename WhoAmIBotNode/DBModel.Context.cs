﻿//------------------------------------------------------------------------------
// <auto-generated>
//     Der Code wurde von einer Vorlage generiert.
//
//     Manuelle Änderungen an dieser Datei führen möglicherweise zu unerwartetem Verhalten der Anwendung.
//     Manuelle Änderungen an dieser Datei werden überschrieben, wenn der Code neu generiert wird.
// </auto-generated>
//------------------------------------------------------------------------------

namespace WhoAmIBotSpace
{
    using System;
    using System.Data.Entity;
    using System.Data.Entity.Infrastructure;
    
    public partial class WhoAmIBotContext : DbContext
    {
        public WhoAmIBotContext()
            : base("name=WhoAmIBotContext")
        {
        }
    
        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            throw new UnintentionalCodeFirstException();
        }
    
        public virtual DbSet<Command> Commands { get; set; }
        public virtual DbSet<ExistingLanguage> ExistingLanguages { get; set; }
        public virtual DbSet<Game> Games { get; set; }
        public virtual DbSet<GamesFinished> GamesFinisheds { get; set; }
        public virtual DbSet<GlobalAdmin> GlobalAdmins { get; set; }
        public virtual DbSet<Group> Groups { get; set; }
        public virtual DbSet<Nextgame> Nextgames { get; set; }
        public virtual DbSet<User> Users { get; set; }
    }
}
