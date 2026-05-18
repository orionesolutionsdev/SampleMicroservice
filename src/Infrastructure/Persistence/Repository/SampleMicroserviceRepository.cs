using Ardalis.Specification.EntityFrameworkCore;
using SampleMicroservice.Application.Common.Persistence;
using SampleMicroservice.Domain.Common;
using SampleMicroservice.Infrastructure.Persistence.Context;

namespace SampleMicroservice.Infrastructure.Persistence.Repository;

public class SampleMicroserviceRepository<T> : RepositoryBase<T>, IRepository<T>, IReadRepository<T>
    where T : class, IAggregateRoot
{
    public SampleMicroserviceRepository(SampleMicroserviceDbContext dbContext)
        : base(dbContext)
    {
    }
}
