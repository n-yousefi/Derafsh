using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Derafsh.Internal;
using Derafsh.Mapping;

namespace Derafsh.Tests;

public class MappingConventionTests
{
    [Fact]
    public void Integer_key_is_generated_by_convention()
    {
        var mapping = MappingCache.Get<GeneratedConvention>();
        Assert.True(mapping.Key.IsGenerated);
    }

    [Fact]
    public void DatabaseGenerated_none_keeps_integer_key_in_insert()
    {
        var mapping = MappingCache.Get<AssignedKey>();
        var statement = SqlServerStatementBuilder.Insert(mapping, new AssignedKey { Id = 42, Name = "fixed" });

        Assert.False(mapping.Key.IsGenerated);
        Assert.Contains("[Id]", statement.Sql);
    }

    [Fact]
    public void Select_aliases_column_names_back_to_property_names()
    {
        var mapping = MappingCache.Get<RenamedColumn>();
        var sql = SqlServerStatementBuilder.SelectByKey(mapping);

        Assert.Contains("[display_name] AS [Name]", sql);
    }

    [Fact]
    public void Default_table_name_matches_clr_type_exactly()
    {
        var mapping = MappingCache.Get<CustomerDto>();
        Assert.Equal(nameof(CustomerDto), mapping.TableName);
    }

    [Fact]
    public void Explicit_table_mapping_is_not_affected_by_dto_suffix()
    {
        var mapping = MappingCache.Get<ExplicitCustomerDto>();
        Assert.Equal("Customer", mapping.TableName);
    }

    [Fact]
    public void Sql_identifiers_escape_closing_brackets()
    {
        Assert.Equal("[a]]b]", SqlIdentifier.Quote("a]b"));
    }

    private sealed class CustomerDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    [Table("Customer")]
    private sealed class ExplicitCustomerDto
    {
        [Key] public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private sealed class GeneratedConvention
    {
        [Key] public int Id { get; set; }
    }

    private sealed class AssignedKey
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    private sealed class RenamedColumn
    {
        [Key] public int Id { get; set; }
        [Column("display_name")] public string Name { get; set; } = "";
    }
}
