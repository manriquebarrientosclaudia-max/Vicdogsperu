namespace VicdogsPeru.Data;

public static class Busqueda
{
    /// <summary>Patrón para EF.Functions.ILike: contiene el texto, sin distinguir mayúsculas, escapando % y _.</summary>
    public static string Contiene(string q) =>
        "%" + q.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
}
