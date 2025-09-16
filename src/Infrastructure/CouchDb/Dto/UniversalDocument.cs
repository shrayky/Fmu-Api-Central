using CouchDB.Driver.Types;
using Newtonsoft.Json;

namespace CouchDb.Dto
{
    public class UniversalDocument<T>: CouchDocument where T: class
    {
        [JsonProperty("data")]
        public required T Data { get; set; }
        public T ToDomain() => Data;
        public static UniversalDocument<T> FromDomain(T entity, string? id) => new UniversalDocument<T> { Data = entity, Id = id };
    }
}
