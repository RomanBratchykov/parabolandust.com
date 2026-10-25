using Microsoft.EntityFrameworkCore;
using Newsletters.Domain.Specifications;

namespace Newsletters.Dal.Repositories;

public static class SpecificationEvaluator
{
    public static IQueryable<T> Apply<T>(
        IQueryable<T> query,
        ISpecification<T> specification,
        bool applyPaging = true,
        bool applyIncludes = true,
        bool applyOrdering = true) where T : class
    {
        foreach (var criteria in specification.Criteria)
            query = query.Where(criteria);

        if (applyIncludes)
        {
            foreach (var include in specification.Includes)
                query = query.Include(include);
        }

        if (applyOrdering && specification.OrderBy is not null)
        {
            query = specification.OrderDescending
                ? query.OrderByDescending(specification.OrderBy)
                : query.OrderBy(specification.OrderBy);
        }

        if (applyPaging && specification.Skip is not null)
            query = query.Skip(specification.Skip.Value);
        if (applyPaging && specification.Take is not null)
            query = query.Take(specification.Take.Value);

        return query;
    }
}
