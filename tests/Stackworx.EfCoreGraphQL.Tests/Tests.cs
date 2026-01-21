namespace Stackworx.EfCoreGraphQL.Tests;

using FluentAssertions;
using Stackworx.EfCoreGraphQL.Tests.Data;

public class Tests
{
    private static readonly int Version = 15;
    
    [Fact]
    public async Task TestPrimaryDataLoader()
    {
        await AppDbContext.WithSqliteInMemoryAsync(db =>
        {
            var entity = db.GetEntity<User>();
            var config = DataLoader.FromEntity(db, entity);

            config.Should().BeEquivalentTo(new DataLoader
            {
                LoaderName = "UserById",
                Nullable = false,
                EntityType = typeof(User),
                Type = DataLoader.DataLoaderType.OneToOne,
                KeyType = typeof(int),
                ReferenceField = "Id",
                DbContextType = typeof(AppDbContext),
                IsShadowProperty = false,
                Notes = "Primary Key Data Loader for <see cref=\"Stackworx.EfCoreGraphQL.Tests.Data.User\"/>",
            });

            config.Emit(Version).Should().MatchSource(
                """
                        [DataLoader]
                        public static async Task<IDictionary<int, Stackworx.EfCoreGraphQL.Tests.Data.User>> UserById(
                            IReadOnlyList<int> keys,
                            Stackworx.EfCoreGraphQL.Tests.Data.AppDbContext context,
                            CancellationToken ct)
                        {
                            return await context.Set<Stackworx.EfCoreGraphQL.Tests.Data.User>()
                                .AsNoTracking()
                                .Where(e => keys.Contains(e.Id))
                                .ToDictionaryAsync(e => e.Id, ct);
                        }
                    """);

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task TestOneToOneRequired()
    {
        await AppDbContext.WithSqliteInMemoryAsync(db =>
        {
            var nav = db.GetNavigation<User>(nameof(User.Profile));
            nav.IsOnDependent.Should().BeFalse();
            nav.IsCollection.Should().BeFalse();
            
            var dataLoaderConfig = DataLoader.FromNavigation(db, nav);

            dataLoaderConfig.Should().BeEquivalentTo(new DataLoader
            {
                LoaderName = "UserProfileByUserId",
                Nullable = false,
                EntityType = typeof(UserProfile),
                Type = DataLoader.DataLoaderType.OneToOne,
                KeyType = typeof(int),
                ReferenceField = "UserId",
                DbContextType = typeof(AppDbContext),
                IsShadowProperty = false,
                Notes = "Navigation Data Loader for <see cref=\"Stackworx.EfCoreGraphQL.Tests.Data.UserProfile.User\"/>",
            });
            
            dataLoaderConfig.Emit(Version).Should().MatchSource(
                """
                        [DataLoader]
                        public static async Task<IDictionary<int, Stackworx.EfCoreGraphQL.Tests.Data.UserProfile>> UserProfileByUserId(
                            IReadOnlyList<int> keys,
                            Stackworx.EfCoreGraphQL.Tests.Data.AppDbContext context,
                            CancellationToken ct)
                        {
                            return await context.Set<Stackworx.EfCoreGraphQL.Tests.Data.UserProfile>()
                                .AsNoTracking()
                                .Where(e => keys.Contains(e.UserId))
                                .ToDictionaryAsync(e => e.UserId, ct);
                        }
                    """);

            var fieldConfig = FieldExtension.FromNavigation(db, nav);
            fieldConfig.Should().BeEquivalentTo(new FieldExtension
            {
                ReferenceField = "Id",
                ReferenceFieldNullable = false,
                DbContextType = typeof(AppDbContext),
                Collection = false,
                ParentType = typeof(User),
                ChildType = typeof(UserProfile),
                ChildTypeNullable = false,
                NavigationName = "Profile",
                LoaderName = "IUserProfileByUserIdDataLoader",
                Notes = "GraphQL Field Override for <see cref=\"Stackworx.EfCoreGraphQL.Tests.Data.User.Profile\"/>",
                IsShadowProperty = false,
            });

            fieldConfig.Emit().Should().MatchSource(
                """
                        public static async Task<Stackworx.EfCoreGraphQL.Tests.Data.UserProfile> GetProfileAsync(
                            [Parent] Stackworx.EfCoreGraphQL.Tests.Data.User parent,
                            IUserProfileByUserIdDataLoader loader,
                            CancellationToken ct)
                        {
                            return await loader.LoadAsync(parent.Id, ct);
                        }
                    """);
            
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task TestOneToOneRequiredInverse()
    {
        await AppDbContext.WithSqliteInMemoryAsync(db =>
        {
            var nav = db.GetNavigation<UserProfile>(nameof(UserProfile.User));
            nav.IsOnDependent.Should().BeTrue();
            nav.IsCollection.Should().BeFalse();

            var config = DataLoader.FromNavigation(db, nav);

            config.Should().BeEquivalentTo(new DataLoader
            {
                LoaderName = "UserById",
                EntityType = typeof(User),
                Nullable = false,
                Type = DataLoader.DataLoaderType.OneToOne,
                KeyType = typeof(int),
                ReferenceField = "Id",
                DbContextType = typeof(AppDbContext),
                IsShadowProperty = false,
                Notes = "Navigation Data Loader for <see cref=\"Stackworx.EfCoreGraphQL.Tests.Data.User.Profile\"/>",
            });
            
            config.Emit(Version).Should().MatchSource(
                """
                        [DataLoader]
                        public static async Task<IDictionary<int, Stackworx.EfCoreGraphQL.Tests.Data.User>> UserById(
                            IReadOnlyList<int> keys,
                            Stackworx.EfCoreGraphQL.Tests.Data.AppDbContext context,
                            CancellationToken ct)
                        {
                            return await context.Set<Stackworx.EfCoreGraphQL.Tests.Data.User>()
                                .AsNoTracking()
                                .Where(e => keys.Contains(e.Id))
                                .ToDictionaryAsync(e => e.Id, ct);
                        }
                    """);

            var fieldConfig = FieldExtension.FromNavigation(db, nav);
            fieldConfig.Should().BeEquivalentTo(new FieldExtension
            {
                ReferenceField = "UserId",
                ReferenceFieldNullable = false,
                DbContextType = typeof(AppDbContext),
                Collection = false,
                ParentType = typeof(UserProfile),
                ChildType = typeof(User),
                ChildTypeNullable = false,
                NavigationName = "User",
                LoaderName = "IUserByIdDataLoader",
                Notes = "GraphQL Field Override for <see cref=\"Stackworx.EfCoreGraphQL.Tests.Data.UserProfile.User\"/>",
                IsShadowProperty = false,
            });
            
            fieldConfig.Emit().Should().MatchSource(
                """
                    public static async Task<Stackworx.EfCoreGraphQL.Tests.Data.User> GetUserAsync(
                        [Parent] Stackworx.EfCoreGraphQL.Tests.Data.UserProfile parent,
                        IUserByIdDataLoader loader,
                        CancellationToken ct)
                    {
                        return await loader.LoadAsync(parent.UserId, ct);
                    }
                """);
            
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task TestOneToOneOptional()
    {
        await AppDbContext.WithSqliteInMemoryAsync(db =>
        {
            var nav = db.GetNavigation<Person>(nameof(Person.Passport));
            nav.IsOnDependent.Should().BeFalse();
            nav.IsCollection.Should().BeFalse();

            DataLoader.FromNavigation(db, nav).Should().BeEquivalentTo(new DataLoader
            {
                LoaderName = "PassportByPersonId",
                EntityType = typeof(Passport),
                Nullable = true,
                Type = DataLoader.DataLoaderType.OneToOne,
                KeyType = typeof(int),
                ReferenceField = "PersonId",
                DbContextType = typeof(AppDbContext),
                IsShadowProperty = false,
                Notes = "Navigation Data Loader for <see cref=\"Stackworx.EfCoreGraphQL.Tests.Data.Passport.Person\"/>",
            });

            var fieldConfig = FieldExtension.FromNavigation(db, nav);
            fieldConfig.Should().BeEquivalentTo(new FieldExtension
            {
                ReferenceField = "Id",
                ReferenceFieldNullable = false,
                DbContextType = typeof(AppDbContext),
                Collection = false,
                ParentType = typeof(Person),
                ChildType = typeof(Passport),
                ChildTypeNullable = true,
                NavigationName = "Passport",
                LoaderName = "IPassportByPersonIdDataLoader",
                Notes = "GraphQL Field Override for <see cref=\"Stackworx.EfCoreGraphQL.Tests.Data.Person.Passport\"/>",
                IsShadowProperty = false,
            });

            fieldConfig.Emit().Should().MatchSource(
                """
                    public static async Task<Stackworx.EfCoreGraphQL.Tests.Data.Passport?> GetPassportAsync(
                        [Parent] Stackworx.EfCoreGraphQL.Tests.Data.Person parent,
                        IPassportByPersonIdDataLoader loader,
                        CancellationToken ct)
                    {
                        return await loader.LoadAsync(parent.Id, ct);
                    }
                """);

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task TestOneToMany()
    {
        await AppDbContext.WithSqliteInMemoryAsync(db =>
        {
            var nav = db.GetNavigation<Post>(nameof(Post.Comments));
            nav.IsOnDependent.Should().BeFalse();
            nav.IsCollection.Should().BeTrue();

            var config = DataLoader.FromNavigation(db, nav);
            config.Should().BeEquivalentTo(new DataLoader
            {
                LoaderName = "CommentsByPostId",
                EntityType = typeof(Comment),
                Nullable = true,
                Type = DataLoader.DataLoaderType.OneToMany,
                KeyType = typeof(int),
                ReferenceField = "PostId",
                DbContextType = typeof(AppDbContext),
                IsShadowProperty = false,
                Notes = "Navigation Data Loader for <see cref=\"Stackworx.EfCoreGraphQL.Tests.Data.Comment.Post\"/>",
            });

            config.Emit(Version).Should().MatchSource(
                """
                        [DataLoader]
                        public static async Task<ILookup<int, Stackworx.EfCoreGraphQL.Tests.Data.Comment>> CommentsByPostId(
                            IReadOnlyList<int> keys,
                            Stackworx.EfCoreGraphQL.Tests.Data.AppDbContext context,
                            CancellationToken ct)
                        {
                            var items = await context.Set<Stackworx.EfCoreGraphQL.Tests.Data.Comment>()
                                .AsNoTracking()
                                .Where(e => keys.Contains(e.PostId!.Value))
                                .ToListAsync(ct);

                            return items.ToLookup(e => e.PostId!.Value);
                        }
                    """);

            var fieldConfig = FieldExtension.FromNavigation(db, nav);
            fieldConfig.Should().BeEquivalentTo(new FieldExtension
            {
                ReferenceField = "Id",
                ReferenceFieldNullable = false,
                DbContextType = typeof(AppDbContext),
                Collection = true,
                ParentType = typeof(Post),
                ChildType = typeof(Comment),
                ChildTypeNullable = false,
                NavigationName = "Comments",
                LoaderName = "ICommentsByPostIdDataLoader",
                Notes = "GraphQL Field Override for <see cref=\"Stackworx.EfCoreGraphQL.Tests.Data.Post.Comments\"/>",
                IsShadowProperty = false,
            });
            
            fieldConfig.Emit().Should().MatchSource(
                """
                    public static async Task<IList<Stackworx.EfCoreGraphQL.Tests.Data.Comment>> GetCommentsAsync(
                        [Parent] Stackworx.EfCoreGraphQL.Tests.Data.Post parent,
                        ICommentsByPostIdDataLoader loader,
                        CancellationToken ct)
                    {
                        return await loader.LoadAsync(parent.Id, ct);
                    }
                """);
            
            return Task.CompletedTask;
        });
    }
    
    [Fact]
    public async Task TestManyToMany()
    {
        await AppDbContext.WithSqliteInMemoryAsync(db =>
        {
            var nav = db.GetSkipNavigation<Post>(nameof(Post.Tags));
            nav.IsOnDependent.Should().BeFalse();
            nav.IsCollection.Should().BeTrue();

            ManyToMany.FromNavigation(db, nav);
            var manyToMany = ManyToMany.FromNavigation(db, nav);
            manyToMany.Should().BeEquivalentTo(new ManyToMany
            {
                LoaderName = "TagsByPosts",
                ChildPropertyName = "Tags",
                ChildKeyName = "Id",
                ChildKeyType = typeof(int),
                ChildType = typeof(Tag),
                ParentPropertyName = "Posts",
                ParentKeyName = "Id",
                ParentKeyType = typeof(int),
                ParentType = typeof(Post),
                DbContextType = typeof(AppDbContext),
            });

            manyToMany.EmitDataLoader().Should().MatchSource(
                """
                        [DataLoader]
                        public static async Task<ILookup<int, Stackworx.EfCoreGraphQL.Tests.Data.Tag>> TagsByPosts(
                            IReadOnlyList<int> keys,
                            Stackworx.EfCoreGraphQL.Tests.Data.AppDbContext context,
                            CancellationToken ct)
                        {
                            var pairs = await context.Set<Stackworx.EfCoreGraphQL.Tests.Data.Tag>()
                                .Where(e => e.Posts.Any(p => keys.Contains(p.Id)))
                                .SelectMany(child => child.Posts.Select(parent => new { parent.Id, Child = child }))
                                .AsNoTracking()
                                .ToListAsync(ct);

                            return pairs.ToLookup(e => e.Id, x => x.Child);
                        }
                    """);

            manyToMany.EmitFieldExtension().Should().MatchSource(
                """
                    public static async Task<Stackworx.EfCoreGraphQL.Tests.Data.Tag[]> GetTagsAsync(
                        [Parent] Stackworx.EfCoreGraphQL.Tests.Data.Post parent,
                        ITagsByPostsDataLoader loader,
                        CancellationToken ct)
                    {
                        return await loader.LoadAsync(parent.Id, ct);
                    }
                """);
            
            return Task.CompletedTask;
        });
    }
    
    [Fact]
    public async Task TestManyToMany_Inverse()
    {
        await AppDbContext.WithSqliteInMemoryAsync(db =>
        {
            var nav = db.GetSkipNavigation<Tag>(nameof(Tag.Posts));
            nav.IsOnDependent.Should().BeFalse();
            nav.IsCollection.Should().BeTrue();

                        ManyToMany.FromNavigation(db, nav);
            var manyToMany = ManyToMany.FromNavigation(db, nav);
            manyToMany.Should().BeEquivalentTo(new ManyToMany
            {
                LoaderName = "PostsByTags",
                ChildPropertyName = "Posts",
                ChildKeyName = "Id",
                ChildKeyType = typeof(int),
                ChildType = typeof(Post),
                ParentPropertyName = "Tags",
                ParentKeyName = "Id",
                ParentKeyType = typeof(int),
                ParentType = typeof(Tag),
                DbContextType = typeof(AppDbContext),
            });

            manyToMany.EmitDataLoader().Should().MatchSource(
                """
                        [DataLoader]
                        public static async Task<ILookup<int, Stackworx.EfCoreGraphQL.Tests.Data.Post>> PostsByTags(
                            IReadOnlyList<int> keys,
                            Stackworx.EfCoreGraphQL.Tests.Data.AppDbContext context,
                            CancellationToken ct)
                        {
                            var pairs = await context.Set<Stackworx.EfCoreGraphQL.Tests.Data.Post>()
                                .Where(e => e.Tags.Any(p => keys.Contains(p.Id)))
                                .SelectMany(child => child.Tags.Select(parent => new { parent.Id, Child = child }))
                                .AsNoTracking()
                                .ToListAsync(ct);

                            return pairs.ToLookup(e => e.Id, x => x.Child);
                        }
                    """);

            manyToMany.EmitFieldExtension().Should().MatchSource(
                """
                    public static async Task<Stackworx.EfCoreGraphQL.Tests.Data.Post[]> GetPostsAsync(
                        [Parent] Stackworx.EfCoreGraphQL.Tests.Data.Tag parent,
                        IPostsByTagsDataLoader loader,
                        CancellationToken ct)
                    {
                        return await loader.LoadAsync(parent.Id, ct);
                    }
                """);
            
            return Task.CompletedTask;
        });
    }

    [Fact(Skip = "Fails")]
    public async Task TestOneToMany_CompositePrimaryKey()
    {
        await AppDbContext.WithSqliteInMemoryAsync(db =>
        {
            var nav = db.GetNavigation<Order>(nameof(Order.Items));
            var config = DataLoader.FromNavigation(db, nav);

            config.Should().BeEquivalentTo(new DataLoader
            {
                LoaderName = "GetPostByComments",
                EntityType = typeof(OrderItem),
                Nullable = false,
                Type = DataLoader.DataLoaderType.OneToMany,
                KeyType = typeof(int),
                ReferenceField = "Id",
                DbContextType = typeof(AppDbContext),
                IsShadowProperty = false,
            });

            return Task.CompletedTask;
        });
    }
}