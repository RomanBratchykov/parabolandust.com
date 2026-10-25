using Newsletters.Bll.Contracts;
using Newsletters.Domain.Models;
using Newsletters.Domain.Specifications;

namespace Newsletters.Bll.Specifications;

public sealed class NewslettersFilterSpecification : Specification<Newsletter>
{
    public NewslettersFilterSpecification(NewsletterQuery query, int pageSize)
    {
        AddCriteria(newsletter => !newsletter.IsDeleted);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            AddCriteria(newsletter => newsletter.Title.Contains(search));
        }

        if (query.AdminId is not null)
            AddCriteria(newsletter => newsletter.AdminId == query.AdminId.Value);
        if (query.IsHidden is not null)
            AddCriteria(newsletter => newsletter.IsHidden == query.IsHidden.Value);
        if (query.PublishedAfter is not null)
            AddCriteria(newsletter => newsletter.PublishedAt >= query.PublishedAfter.Value);

        AddInclude(nameof(Newsletter.DeliverySettings));
        SetOrder(GetSortExpression(query.SortBy), string.Equals(query.SortDir, "desc", StringComparison.OrdinalIgnoreCase));
        SetPagination((query.Page - 1) * pageSize, pageSize);
    }

    private static System.Linq.Expressions.Expression<Func<Newsletter, object?>> GetSortExpression(string sortBy)
        => sortBy.ToLowerInvariant() switch
        {
            "title" => newsletter => newsletter.Title,
            "publishedat" => newsletter => newsletter.PublishedAt,
            _ => newsletter => newsletter.CreatedAt
        };
}
