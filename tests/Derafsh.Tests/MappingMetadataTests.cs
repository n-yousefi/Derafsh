using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Derafsh.Mapping;

namespace Derafsh.Tests;

public class MappingMetadataTests
{
    [Fact]
    public void Builds_reference_and_child_collection_metadata()
    {
        var mapping = MappingCache.Get<Identity>();

        Assert.Equal("Identity", mapping.TableName);
        Assert.Equal("dbo", mapping.Schema);
        Assert.Equal(nameof(Identity.Id), mapping.Key.Property.Name);
        Assert.True(mapping.Key.IsGenerated);

        var reference = Assert.Single(mapping.References);
        Assert.Equal(nameof(Identity.Person), reference.NavigationProperty.Name);
        Assert.Equal(nameof(Identity.PersonId), reference.ForeignKey.Property.Name);

        var children = Assert.Single(mapping.ChildCollections);
        Assert.Equal(nameof(Identity.Phones), children.CollectionProperty.Name);
        Assert.Equal(nameof(Phone.IdentityId), children.ForeignKeyPropertyName);
    }

    [Fact]
    public void Rejects_composite_keys()
    {
        Assert.Throws<DerafshMappingException>(() => MappingCache.Get<Composite>());
    }

    [Fact]
    public void Rejects_getter_only_reference_navigation()
    {
        var error = Assert.Throws<DerafshMappingException>(() => MappingCache.Get<GetterOnlyReferenceOwner>());
        Assert.Contains("must be writable", error.Message);
    }

    [Table("Identity")]
    private sealed class Identity
    {
        [Key]
        public int Id { get; set; }
        public int? PersonId { get; set; }
        [Reference(nameof(PersonId))]
        public Person? Person { get; set; }
        [ChildCollection(nameof(Phone.IdentityId))]
        public List<Phone> Phones { get; set; } = [];
    }

    private sealed class Person
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    private sealed class Phone
    {
        [Key]
        public int Id { get; set; }
        public int IdentityId { get; set; }
        public string Number { get; set; } = "";
    }

    private sealed class Composite
    {
        [Key] public int A { get; set; }
        [Key] public int B { get; set; }
    }

    private sealed class GetterOnlyReferenceOwner
    {
        [Key] public int Id { get; set; }
        public int PersonId { get; set; }
        [Reference(nameof(PersonId))]
        public Person? Person { get; }
    }
}
