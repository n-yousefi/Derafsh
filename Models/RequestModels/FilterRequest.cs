
namespace Derafsh.Models.RequestModels
{
    public class FilterRequest
    {
        public FilterRequest()
        {
            
        }
        public FilterRequest(int pageNumber, int pageSize, string sort,string sortDirection, string searchPhrase)
        {
            PageNumber = pageNumber;
            PageSize = pageSize;
            Sort = sort;
            SortDirection = sortDirection;
            SearchPhrase = searchPhrase;
        }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public string Sort { get; set; }
        public string SortDirection { get; set; }
        public string SearchPhrase { get; set; }
    }
}
