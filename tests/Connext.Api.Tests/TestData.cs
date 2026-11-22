using Connext.Data.Entities;

public static class TestData
{
    // Đủ 4 trường required; Phone/Email unique theo id
    public static User NewUser(Guid id) => new()
    {
        Id = id,
        FullName = "User " + id.ToString("N")[..6],
        Phone = id.ToString("N")[..10],
        Email = $"{id:N}@test.local",
        PasswordHash = "x"
    };
}