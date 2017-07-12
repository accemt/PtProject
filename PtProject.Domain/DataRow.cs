using System.Linq;

namespace PtProject.Domain
{
    public class DataRow<T>
    {
        public string Id;
        public T Target;
        public T[] Values;

        public static explicit operator DataRow<object>(DataRow<T> row)
        {
            var nrow = new DataRow<object>();
            nrow.Id = row.Id;
            nrow.Target = row.Target;
            nrow.Values = row.Values.Cast<object>().ToArray();
            return nrow;
        }

        public override string ToString()
        {
            return Id + ": " + Target;
        }
    }
}
