﻿// <auto-generated />
using System;
using DiscordBot;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace DiscordBot.Migrations
{
    [DbContext(typeof(AppDBContext))]
    [Migration("20230703044458_RestofShameRename")]
    partial class RestofShameRename
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "7.0.8")
                .HasAnnotation("Relational:MaxIdentifierLength", 128);

            SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder);

            modelBuilder.Entity("DiscordBot.BirthdayDef", b =>
                {
                    b.Property<decimal>("UserId")
                        .HasColumnType("decimal(20,0)");

                    b.Property<decimal>("GuildId")
                        .HasColumnType("decimal(20,0)");

                    b.Property<string>("CreatedBy")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTime>("Date")
                        .HasColumnType("datetime2");

                    b.Property<string>("TimeZone")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("UserId", "GuildId");

                    b.ToTable("BirthdayDefs");
                });

            modelBuilder.Entity("DiscordBot.DiscordGuildUser", b =>
                {
                    b.Property<decimal>("Id")
                        .HasColumnType("decimal(20,0)");

                    b.Property<decimal>("GuildId")
                        .HasColumnType("decimal(20,0)");

                    b.Property<string>("Nickname")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Username")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("Id", "GuildId");

                    b.ToTable("GuildUsers");
                });

            modelBuilder.Entity("DiscordBot.DiscordLog", b =>
                {
                    b.Property<string>("Type")
                        .HasColumnType("nvarchar(450)");

                    b.Property<decimal>("MessageId")
                        .HasColumnType("decimal(20,0)");

                    b.Property<DateTimeOffset?>("Date")
                        .HasColumnType("datetimeoffset");

                    b.HasKey("Type", "MessageId");

                    b.HasIndex("MessageId");

                    b.ToTable("DiscordLog");
                });

            modelBuilder.Entity("DiscordBot.DiscordMessage", b =>
                {
                    b.Property<decimal>("Id")
                        .HasColumnType("decimal(20,0)");

                    b.Property<decimal?>("AuthorId")
                        .HasColumnType("decimal(20,0)");

                    b.Property<string>("AuthorMention")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<decimal>("ChannelId")
                        .HasColumnType("decimal(20,0)");

                    b.Property<string>("ChannelMention")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("CleanContent")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Content")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<DateTimeOffset?>("EditedTimestamp")
                        .HasColumnType("datetimeoffset");

                    b.Property<decimal>("GuildId")
                        .HasColumnType("decimal(20,0)");

                    b.Property<bool>("IsPinned")
                        .HasColumnType("bit");

                    b.Property<bool>("IsSuppressed")
                        .HasColumnType("bit");

                    b.Property<bool>("IsTTS")
                        .HasColumnType("bit");

                    b.Property<bool>("MentionedEveryone")
                        .HasColumnType("bit");

                    b.Property<decimal?>("ReferenceId")
                        .HasColumnType("decimal(20,0)");

                    b.Property<int>("Source")
                        .HasColumnType("int");

                    b.Property<decimal?>("ThreadID")
                        .HasColumnType("decimal(20,0)");

                    b.Property<string>("ThreadMention")
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTimeOffset>("Timestamp")
                        .HasColumnType("datetimeoffset");

                    b.Property<int>("Type")
                        .HasColumnType("int");

                    b.HasKey("Id");

                    b.ToTable("UserMessages");
                });

            modelBuilder.Entity("DiscordBot.DiscordLog", b =>
                {
                    b.HasOne("DiscordBot.DiscordMessage", "Message")
                        .WithMany("DiscordLogs")
                        .HasForeignKey("MessageId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Message");
                });

            modelBuilder.Entity("DiscordBot.DiscordMessage", b =>
                {
                    b.Navigation("DiscordLogs");
                });
#pragma warning restore 612, 618
        }
    }
}
