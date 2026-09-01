using Microsoft.AspNetCore.Mvc.RazorPages;
using TallerMecanico.Data;

namespace TallerMecanico.Pages;

public class IndexModel : PageModel
{
    private readonly DatabaseConnection _databaseConnection;

    public IndexModel(DatabaseConnection databaseConnection)
    {
        _databaseConnection = databaseConnection;
    }

    public void OnGet()
    {
        using var connection = _databaseConnection.CreateConnection();

        connection.Open();
    }
}