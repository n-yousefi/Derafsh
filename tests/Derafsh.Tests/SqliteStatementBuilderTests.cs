using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Derafsh.Internal;
using Derafsh.Mapping;

namespace Derafsh.Tests;

public sealed class SqliteStatementBuilderTests
{
    [Fact]
    public void Insert_uses_returning_for_generated_key_and_omits_schema()
    {
        var mapping = MappingCache.Get<Person>();
        var statement = SqliteStatementBuilder.Insert(mapping, new Person { Name = "Ada" });

        Assert.Contains("INSERT INTO [Person]", statement.Sql);
        Assert.DoesNotContain("[custom].[Person]", statement.Sql);
        Assert.Contains("RETURNING [Id]", statement.Sql);
    }

    [Fact]
    public void Key_only_update_uses_existence_check_instead_of_fake_update()
    {
        var mapping = MappingCache.Get<KeyOnly>();

        Assert.Null(SqliteStatementBuilder.Update(mapping, new KeyOnly { Id = 7 }));
        var exists = SqliteStatementBuilder.ExistsByKey(mapping, new KeyOnly { Id = 7 });
        Assert.Contains("SELECT COUNT(1)", exists.Sql);
        Assert.Contains("WHERE [Id] = @key", exists.Sql);
    }

    [Table("Person", Schema = "custom")]
    private sealed class Person
    {
        [Key] public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private sealed class KeyOnly
    {
        [Key] public int Id { get; set; }
    }
}
