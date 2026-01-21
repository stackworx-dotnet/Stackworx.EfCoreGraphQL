namespace Stackworx.EfCoreGraphQL.Tests.Data;

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

public class AppDbContext : DbContext
{
    public DbSet<Author> Authors => this.Set<Author>();

    public DbSet<Book> Books => this.Set<Book>();

    public DbSet<User> Users => this.Set<User>();

    public DbSet<UserProfile> UserProfiles => this.Set<UserProfile>();

    public DbSet<Person> People => this.Set<Person>();

    public DbSet<Passport> Passports => this.Set<Passport>();

    public DbSet<Post> Posts => this.Set<Post>();

    public DbSet<Tag> Tags => this.Set<Tag>();

    public DbSet<Comment> Comments => this.Set<Comment>();

    public DbSet<Student> Students => this.Set<Student>();

    public DbSet<Course> Courses => this.Set<Course>();

    public DbSet<Enrollment> Enrollments => this.Set<Enrollment>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public IEntityType GetEntity<T>()
    {
        var entity = this.Model
            .GetEntityTypes()
            .SingleOrDefault(t => t.ClrType == typeof(T));

        if (entity is null)
        {
            throw new ArgumentException($"Entity type {typeof(T).Name} not found in model");
        }

        return entity;
    }

    public INavigation GetNavigation<T>(string name)
    {
        var entity = this.GetEntity<T>();
        var navigations = entity
            .GetNavigations()
            .ToList();

        var nav = navigations
            .SingleOrDefault(n => n.Name == name);
        
        if (nav is null)
        {
            throw new ArgumentException(
                $"Navigation {name} not found on entity type {typeof(T).Name}. Navigations: {string.Join(",", navigations.Select(n => n.Name))}");
        }

        return nav;
    }

    // -------------------------
    // Model configuration
    // -------------------------
    protected override void OnModelCreating(ModelBuilder model)
    {
        base.OnModelCreating(model);

        // 1 : Many (required FK)
        model.Entity<Author>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Name).IsRequired();
            b.HasMany(x => x.Books)
                .WithOne(x => x.Author)
                .HasForeignKey(x => x.AuthorId) // explicit FK property
                .OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<Book>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Title).IsRequired();
            b.HasIndex(x => x.AuthorId);
        });

        // 1 : 1 (required) via unique FK (not shared PK)
        model.Entity<User>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Email).IsRequired();
            b.HasOne(x => x.Profile)
                .WithOne(x => x.User)
                .HasForeignKey<UserProfile>(x => x.UserId) // explicit FK
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<UserProfile>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.DisplayName).IsRequired(false);
            b.HasIndex(x => x.UserId).IsUnique(); // enforce 1:1
        });

        // 1 : 1 (optional/nullable)
        // Person may or may not have a Passport
        model.Entity<Person>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Name).IsRequired();
            b.HasOne(x => x.Passport)
                .WithOne(x => x.Person)
                .HasForeignKey<Passport>(x => x.PersonId) // explicit FK
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);
        });

        model.Entity<Passport>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Number).IsRequired();
            b.HasIndex(x => x.PersonId).IsUnique();
        });

        // Many : Many (skip navigations)
        model.Entity<Post>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Title).IsRequired();
            // Shadow-FK example below on Comment => Post
            b.HasMany(x => x.Tags)
                .WithMany(x => x.Posts)
                .UsingEntity<Dictionary<string, object>>(
                    "PostTag",
                    right => right.HasOne<Tag>().WithMany().HasForeignKey("TagId").OnDelete(DeleteBehavior.Cascade),
                    left => left.HasOne<Post>().WithMany().HasForeignKey("PostId").OnDelete(DeleteBehavior.Cascade),
                    j =>
                    {
                        j.HasKey("PostId", "TagId");
                        j.ToTable("PostTags");
                    });

            // Shadow many to many
            b.HasMany(x => x.TagsShadow)
                .WithMany("PostsShadow");
        });

        model.Entity<Tag>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Name).IsRequired();
        });

        // Shadow FK variation: Comment → Post without a PostId property
        model.Entity<Comment>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Text).IsRequired();
            b.Property<int?>("PostId"); // shadow FK
            b.HasOne(x => x.Post)
                .WithMany(x => x.Comments)
                .HasForeignKey("PostId")
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasIndex("PostId");
        });

        // Many : Many with payload (explicit join entity)
        model.Entity<Student>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Name).IsRequired();
        });

        model.Entity<Course>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Title).IsRequired();
        });

        model.Entity<Enrollment>(b =>
        {
            b.HasKey(x => new { x.StudentId, x.CourseId });
            b.HasOne(x => x.Student)
                .WithMany(x => x.Enrollments)
                .HasForeignKey(x => x.StudentId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Course)
                .WithMany(x => x.Enrollments)
                .HasForeignKey(x => x.CourseId)
                .OnDelete(DeleteBehavior.Cascade);
            b.Property(x => x.Grade).IsRequired(false);
        });
        
        model.Entity<Order>(b =>
        {
            b.HasKey(x => new { x.Year, x.Number });
            b.Property(x => x.Customer).IsRequired();
        });

        model.Entity<OrderItem>(b =>
        {
            b.HasKey(x => new { x.OrderYear, x.OrderNumber, x.LineNo });

            b.HasOne(x => x.Order)
                .WithMany(x => x.Items)
                .HasForeignKey(x => new { x.OrderYear, x.OrderNumber })
                .OnDelete(DeleteBehavior.Cascade);

            b.Property(x => x.Sku).IsRequired();
        });
    }

    // -------------------------
    // SQLite in-memory factory
    // -------------------------
    public static async Task<(AppDbContext Context, DbConnection Connection)> CreateSqliteInMemoryAsync(
        bool seed = true)
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .EnableSensitiveDataLogging()
            .Options;

        var ctx = new AppDbContext(options);
        await ctx.Database.EnsureCreatedAsync();

        if (seed)
        {
            await ctx.SeedAsync();
        }

        return (ctx, connection);
    }

    // Simple seeding for DataLoader scenarios
    public async Task SeedAsync()
    {
        var db = this;
        // Authors & Books (1:m)
        var a1 = new Author { Name = "Author A" };
        var a2 = new Author { Name = "Author B" };
        db.Authors.AddRange(a1, a2);
        db.Books.AddRange(
            new Book { Title = "A-1", Author = a1 },
            new Book { Title = "A-2", Author = a1 },
            new Book { Title = "B-1", Author = a2 }
        );

        // Users & UserProfiles (1:1 required)
        var u1 = new User { Email = "alice@example.com" };
        var u2 = new User { Email = "bob@example.com" };
        db.Users.AddRange(u1, u2);
        db.UserProfiles.AddRange(
            new UserProfile { User = u1, DisplayName = "Alice" },
            new UserProfile { User = u2, DisplayName = "Bob" }
        );

        // Persons & Passports (1:1 optional)
        var p1 = new Person { Name = "Cara" };
        var p2 = new Person { Name = "Dan" };
        db.People.AddRange(p1, p2);
        db.Passports.AddRange(
            new Passport { Number = "X123", Person = p1 } // Dan has no passport
        );

        // Posts, Tags, Comments (m:m + shadow FK)
        var post1 = new Post { Title = "EF Tips" };
        var post2 = new Post { Title = "GraphQL Tricks" };
        var tagEf = new Tag { Name = "efcore" };
        var tagGql = new Tag { Name = "graphql" };
        post1.Tags.Add(tagEf);
        post2.Tags.Add(tagGql);
        db.Posts.AddRange(post1, post2);
        db.Tags.AddRange(tagEf, tagGql);

        // Comments using shadow FK: attach via navigation only
        db.Comments.AddRange(
            new Comment { Text = "Great post", Post = post1 },
            new Comment { Text = "Subtle nuance here", Post = post1 },
            new Comment { Text = "Thanks!", Post = post2 }
        );

        // Students/Courses via Enrollment (m:m with payload)
        var s1 = new Student { Name = "Eve" };
        var s2 = new Student { Name = "Finn" };
        var c1 = new Course { Title = "Databases" };
        var c2 = new Course { Title = "Distributed Systems" };
        db.Students.AddRange(s1, s2);
        db.Courses.AddRange(c1, c2);
        db.Enrollments.AddRange(
            new Enrollment { Student = s1, Course = c1, Grade = 90 },
            new Enrollment { Student = s1, Course = c2, Grade = 86 },
            new Enrollment { Student = s2, Course = c1, Grade = 77 }
        );

        await db.SaveChangesAsync();
    }
}