using Domain.Entitys.Interfaces;

namespace Domain.Entitys
{
    public class UserEntity: IHaveStringId
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
