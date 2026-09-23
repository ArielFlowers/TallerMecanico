namespace TallerMecanico.Data;

public interface IRepository
{
}

public interface IRepository<T> : IRepository
{
    List<T> GetAll();

    T? GetById(int id);

    void Add(T entity);

    void Update(T entity);

    void Delete(int id);

    int Count();
}
