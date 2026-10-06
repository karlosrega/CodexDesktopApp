using System;
using System.Data;
using System.Data.SqlClient;

namespace AdmitOne
{
    public sealed class Database
    {
        private readonly string connectionString;
        private readonly SqlConnection shared;
        private readonly SqlTransaction transaction;
        public Database(string connectionString) { this.connectionString=connectionString; }
        // Test-only injection permits complete rollback of test changes in the real schema.
        public Database(SqlConnection connection,SqlTransaction transaction) { shared=connection; this.transaction=transaction; }
        public static SqlParameter P(string name,SqlDbType type,object value,int size=0)
        {
            var p=new SqlParameter(name,type); if(size!=0) p.Size=size; p.Value=value??DBNull.Value; return p;
        }
        public DataTable Call(string procedure,params SqlParameter[] parameters)
        {
            using(var owned=shared==null ? new SqlConnection(connectionString) : null)
            {
                var connection=shared??owned;
                if(connection.State!=ConnectionState.Open) connection.Open();
                using(var cmd=new SqlCommand(procedure,connection,transaction))
                {
                    cmd.CommandType=CommandType.StoredProcedure; cmd.CommandTimeout=30; cmd.Parameters.AddRange(parameters);
                    try { using(var reader=cmd.ExecuteReader()) { var table=new DataTable(); table.Load(reader); return table; } }
                    catch(SqlException e)
                    {
                        if(e.Number>=50000 && e.Number<50100) throw new BusinessException(e.Message,e.Number);
                        if(e.Number==2601 || e.Number==2627) throw new BusinessException("Ya existe un registro con ese nombre o usuario.",50006);
                        if(e.Number==547) throw new BusinessException("Los datos no cumplen las restricciones o el perfil ya no existe.");
                        throw;
                    }
                }
            }
        }
    }
}
