using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Derafsh.Internal;
using Derafsh.Mapping;

namespace Derafsh.Tests;

public class SqlServerStatementBuilderTests
{
    [Fact]
    public void Insert_is_parameterized_and_returns_generated_key()
    {
        var mapping = MappingCache.Get<Person>();
        var person = new Person { Name = "Robert'); DROP TABLE Person;--", Age = 32 };

        var statement = SqlServerStatementBuilder.Insert(mapping, person);

        Assert.Contains("SCOPE_IDENTITY()", statement.Sql);
        Assert.DoesNotContain("OUTPUT INSERTED.[Id]", statement.Sql);
        Assert.DoesNotContain(person.Name, statement.Sql);
        Assert.Contains("@p0", statement.Sql);
        Assert.Contains("@p1", statement.Sql);
    }

    [Fact]
    public void Update_never_updates_key_or_computed_columns()
    {
        var mapping = MappingCache.Get<Person>();
        var statement = SqlServerStatementBuilder.Update(mapping, new Person { Id = 10, Name = "Naser", Age = 40 });

        Assert.NotNull(statement);
        Assert.DoesNotContain("SET [Id]", statement.Sql);
        Assert.DoesNotContain("[Version] =", statement.Sql);
        Assert.Contains("WHERE [Id] = @key", statement.Sql);
    }


    [Fact]
    public void Owned_update_checks_the_existing_parent_foreign_key()
    {
        var mapping = MappingCache.Get<OwnedChild>();
        var ownerForeignKey = mapping.ScalarProperties.Single(p => p.Property.Name == nameof(OwnedChild.ParentId));
        var statement = SqlServerStatementBuilder.Update(
            mapping,
            new OwnedChild { Id = 7, ParentId = 3, Value = "updated" },
            ownerForeignKey,
            3);

        Assert.NotNull(statement);
        Assert.Contains("WHERE [Id] = @key AND [ParentId] = @ownerKey", statement.Sql);
    }

    [Fact]
    public void Key_only_update_uses_existence_check_instead_of_fake_update()
    {
        var mapping = MappingCache.Get<KeyOnly>();

        Assert.Null(SqlServerStatementBuilder.Update(mapping, new KeyOnly { Id = 7 }));
        var exists = SqlServerStatementBuilder.ExistsByKey(mapping, new KeyOnly { Id = 7 });
        Assert.Contains("SELECT COUNT(1)", exists.Sql);
        Assert.Contains("WHERE [Id] = @key", exists.Sql);
    }

    [Table("Person")]
    private sealed class Person
    {
        [Key] public int Id { get; set; }
        public string Name { get; set; } = "";
        public int Age { get; set; }
        [Timestamp] public byte[]? Version { get; set; }
    }

    private sealed class OwnedChild
    {
        [Key] public int Id { get; set; }
        public int ParentId { get; set; }
        public string Value { get; set; } = string.Empty;
    }

    private sealed class KeyOnly
    {
        [Key] public int Id { get; set; }
    }
}
