# Derafsh O/RM
Derafsh is an object-relational mapper that enables .NET developers to read or write operations in single statements. 
You can easily read a complex object from (or write to) multiple related tables at SQL Server database.

Definitely, Derafsh does not satisfy all your needs, But it probably will remove a lot of repetitive tasks.
## ViewModels Programming
Everything that Derafsh does is based on the ViewModels (complex objects). You just need to prapare the prepare the ViewModel 
and then Derafsh will do the rest. Perhaps we can call it, ViewModels Programming or ViewModels O/RM. 

## An example
With the help of Derafsh, we can create one insert, edit or index form for all objects and complex objects. I've created a simple example to show the goal of creating Derafsh: 
* [Derafsh-Sample](https://github.com/n-yousefi/Derafsh-Sample)

## Installing
Install as [NuGet package](https://www.nuget.org/packages/Derafsh/):
Package manager:
```powershell
Install-Package Derafsh
```
.NET CLI:
```bash
dotnet add package Derafsh
```
## Getting Started
I'm going to use these tables for my examples:
![Example](https://github.com/n-yousefi/Derafsh/blob/master/diagram.png)
The Identity model:
```c#
public class Identity
{
     [PrimaryKey]
     public int Id {get; set;}
     public int IdentityEnumId { get; set; } 
     [ForeignKey("Person")]
     public int? PersonId { get; set; }
     [ForeignKey("Organization")]
     public int? OrganizationId { get; set; }
     public bool IsActive {get; set;}
     public bool IsDeleted {get; set;}
}
```
### Preparing the ViewModel
With Derafsh you can easily read multiple related tables to a ViewModel List or write a ViewModel to its own tables.
You must create your own object and determine the relations between the tables. 
#### Join 
If your model contains some foreign keys then you can use the Join attribute to specify that relation for Derafsh.
(This means that you can use the Join attribute in One-to-One or Many-To-One relationships)
In the example if I create the ViewModel like this:
```c#
[Join]
public PersonViewModel Person { get; set; }
public OrganizationViewModel Organization { get; set; }
```
Then Derafsh will ignore the Organization but it will read/write the related Person by the Identity.
#### InverseJoin
If you have One-to-Many relationships that means your table primary key is a foreign key in other tables, you can use the
InverseJoin attribute.
In the example if I create the ViewModel as follow:
```c#
[InverseJoin]       
public List<PhoneViewModel> Phone { get; set; }
public List<AddressViewModel> Address { get; set; }
```
Then Derafsh will ignore the Address but it will read/write the list of Phones for the Identity.

Full ViewModel for Identity:
```c#
[Table("Identity")]
public class IdentityViewModel:Models.Identity
{
     private PersonViewModel _person;
     private OrganizationViewModel _organization;
     [Join]
     public PersonViewModel Person
     {
         get => IdentityEnumId != (int) IdentityEnum.Person ? null : _person;
         set => _person = value;
     }
     [Join]
     public OrganizationViewModel Organization
     {
         get => IdentityEnumId != (int) IdentityEnum.Organization ? null : _organization;
         set => _organization = value;
     }
     [InverseJoin]
     public List<PhoneViewModel> Phone { get; set; }
     [InverseJoin]
     public List<AddressViewModel> Address { get; set; }
}
```
### Insert a ViewModel to the database
```c#
Task<int> Insert<T>(object viewModel, CancellationToken cancellationToken=null, SqlTransaction transaction = null);
```
Example usage:
```c#
var identity = new IdentityViewModel()
{
      IdentityEnumId =  (int) IdentityEnum.Person,
      Person = new PersonViewModel()
      {
           FirstName = "Naser",
           LastName = "Yousefi",
           BirthCertificatedCityId = cityId,
           ...
      },
      Address = new List<AddressViewModel>()
      {
            new AddressViewModel()
            {
                  FullAddress = "Home: The earth",
                  //...
            },
            new AddressViewModel()
            {
                  FullAddress = "Work: The oceans",
                  //...
            },
       },
       Phone = new List<AddressViewModel>()
       {
            new PhoneViewModel()
            {
                 Number = "+678 768 1217",
                 //...    
            }
       },
       IsActive = true,
       IsDeleted = false
} 
var result = await databaseActions.Insert<IdentityViewModel>(identity, cancellationToken, transaction);
```
### Fill an IEnumerable of ViewModels from the database
```c#
Task<IEnumerable<T>> Select<T>(QueryConditions queryConditions = null, FilterRequest filter = null, SqlTransaction transaction = null);
```
#### Query conditions
You can pass a QueryConditions object that contain your condition for each table. 
QueryConditions Methods:
```c#
var conditions = new QueryConditions();
// Adding a condition for spacefic table:
conditions.AddCondition("TableName", "Condition");
// Adding a condition for all tables that have the mentioned ColumnName.
conditions.AddPublicCondition("ColumnName","Condition");
```
#### Filtering the results
You can filter the results based on your preferments. 
```c#
public FilterRequest(int pageNumber, int pageSize, string sort, string sortDirection, string searchPhrase);
```
Example usage:
```c#
var conditions = new QueryConditions();
conditions.AddCondition("Identity", "id=10");
conditions.AddPublicCondition("IsActive","IsActive=1");
conditions.AddPublicCondition("IsDeleted", "IsDeleted=0");

var filter = new FilterRequest(1,20,"Id","Asc","");
IEnumerable<IdentityViewModel> items = await databaseActions.Select<IdentityViewModel>(conditions, filter);
```
### Finding by id 
```c#
Task<T> Find<T>(int id, QueryConditions queryConditions = null, SqlTransaction transaction = null);
```
Example usage:
```c#
var conditions = new QueryConditions();
conditions.AddPublicCondition("IsDeleted", "IsDeleted = 0");
var model = await databaseActions.Find<IdentityViewModel>(1000,conditions,transaction);
```
### Showing an Abstract table of ViewModel
Usually you need to display a list of your ViewModel to the users. You can use the Select method with filtering the results but
loading all fields of ViewModels is costly. For this purpose, I've created an attribute and a method called Abstract.
```c#
Task<DataTable> Abstract<T>(string conditions = null, FilterRequest filterRequest = null, SqlTransaction transaction = null);
```
You can use the Abstract attribute over the Join properties (Not over the InversJoins).
**Note:** For now, you can only use this attribute only over the Table columns properties. 

In the Example if I use Abstract attribute over the Person Join then the Person abstract properties will also be seen in the
result.
```c#
[Table("Identity")]
public class IdentityViewModel:Models.Identity
{
     [Abstract]
     [Join]
     public PersonViewModel Person { get; set; }
     [Abstract]
     [Join]
     public OrganizationViewModel Organization { get; set; }
}
[Table("Person")]
public class PersonViewModel:Models.Person
{
     [Abstract]
     [Display(Name = "First Name")]
     public string FirstName { get; set; }
     [Abstract]
     [Display(Name = "Last Name")]
     public string LastName { get; set; }
}
[Table("Organization")]
public class OrganizationViewModel:Models.Organization
{
     [Abstract]
     [Display(Name = "Company")]
     public string Name { get; set; }
     [Abstract]
     [Display(Name = "Registration Number")]
     public string RegistrationNumber { get; set; }
}
```
Example usage:
```c#
var filter = new FilterRequest(1, pageSize, "Id", "Asc", "");
var conditions = "Date > '2012-11-29 18:21:11.123' and Identity.IsActive = 1 and Identity.Isdeleted = 0"
var items = await databaseActions.Abstract<IdentityViewModel>(conditions, filter);
```
|  First Name   |   Last Name   |   Company  | Registration Number |
| ------------- | ------------- | ---------- | ------------------- |
|     null      |      null     | Opt-xa Inc |     3242342352      |
|     Javad     | Hajian-nezhad |    null    |        null         |
|    Mohammad   |  Kheirandish  |    null    |        null         |
|     null      |      null     | Desire Inc |     9873214654      |

### Update a ViewModel in the database
You can pass a ViewModel to Update method and then all VewModel instances will be updated in database.
**Note:** all instances in update method must have an Id property filled with valid value!
Task<int> Update<T>(object viewModel,CancellationToken cancellationToken, SqlTransaction transaction = null);
```c#
var identity = new IdentityViewModel()
{
      Id = 56
      IdentityEnumId =  (int) IdentityEnum.Person,
      Person = new PersonViewModel()
      {
           Id = 120
           FirstName = "Javad",
           LastName = "Hajian-nezhad",
           BirthCertificatedCityId = cityId,
           ...
      },
      Address = new List<AddressViewModel>()
      {
            new AddressViewModel()
            {
                  Id = 79
                  FullAddress = "Home: The galaxy",
                  //...
            },
            new AddressViewModel()
            {
                  Id = 12
                  FullAddress = "Work: The winds",
                  //...
            },
       },
       Phone = new List<AddressViewModel>()
       {
            new PhoneViewModel()
            {
                 Id = 1520
                 Number = "+678 768 1217",
                 //...    
            }
       },
       IsActive = true,
       IsDeleted = false
} 
var result = await databaseActions.Update<IdentityViewModel>(identity, cancellationToken);
```
### Count
```c#
Task<int> Count<T>(string condition = "",CancellationToken cancellationToken=null, SqlTransaction transaction = null);
```
Example usage:
```c#
var count = await databaseActions.Count<IdentityViewModel>("IsDeleted = 0",cancellationToken, transaction);
```
### Soft delete 
Derafsh support soft deleting by setting "IsDeleted" property to true. It's very personalized now 
but feel free to use Derafsh. I will add some options for selecting desired property. For now, you can use update method. 
```c#
Task<int> UpdateByParentSoftDelete<T>(object viewModel, CancellationToken cancellationToken = null);
```
## Authors
<a href="https://stackexchange.com/users/3919456">
<img src="https://stackexchange.com/users/flair/3919456.png" width="208" height="58" alt="profile for Naser Yousefi on Stack Exchange, a network of free, community-driven Q&amp;A sites" title="profile for Naser Yousefi on Stack Exchange, a network of free, community-driven Q&amp;A sites">
</a>
     
 ## License
Licensed under the Apache License, Version 2.0.
