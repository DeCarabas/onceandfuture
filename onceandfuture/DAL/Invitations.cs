namespace OnceAndFuture.DAL
{
    using Npgsql;
    using NpgsqlTypes;
    using System;
    using System.Security.Cryptography;
    using System.Threading.Tasks;

    public class Invitation
    {
        public string Code { get; set; }
        public DateTimeOffset? Expiration { get; set; }
    }

    public class InvitationStore : DalBase
    {
        static readonly RandomNumberGenerator rng = RandomNumberGenerator.Create();

        public InvitationStore() : base("invitations") { }

        static string CreateID()
        {
            const int size = 6;
            byte[] data = new byte[size];
            rng.GetBytes(data);
            return Convert.ToBase64String(data);
        }

        public async Task<Invitation> CreateInvitation(DateTimeOffset? expires)
        {
            string id = CreateID();
            await DoOperation("create", id, async () =>
            {
                using (var connection = await OpenConnection())
                {
                    using (NpgsqlCommand cmd = connection.CreateCommand())
                    {
                        object expireVal = expires.HasValue ? (object)expires.Value : (object)DBNull.Value;

                        cmd.CommandText = @"
                            INSERT INTO invitations (id, expires, created) 
                            VALUES (@id, @expires, current_timestamp)
                        ";
                        cmd.Parameters.AddWithValue("id", NpgsqlDbType.Varchar, id);
                        cmd.Parameters.AddWithValue("expires", NpgsqlDbType.TimestampTZ, expireVal);
                        cmd.Prepare();
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
                return null;
            });
            return new Invitation
            {
                Code = id,
                Expiration = expires,
            };
        }

        public async Task<bool> IsInvitationValid(string id)
        {
            long count = 0;
            await DoOperation("claim", id, async () =>
            {
                using (var connection = await OpenConnection())
                {
                    using (NpgsqlCommand cmd = connection.CreateCommand())
                    {
                        cmd.CommandText = @"
                            SELECT COUNT(1) 
                            FROM invitations 
                            WHERE id = @id 
                              AND claimed IS NULL 
                              AND claimed_id IS NULL
                        ";
                        cmd.Parameters.AddWithValue("id", NpgsqlDbType.Varchar, id);
                        cmd.Prepare();
                        count = (long)(await cmd.ExecuteScalarAsync());
                    }
                }
                return null;
            });
            return count != 0;
        }

        public async Task<bool> TryClaimInvitation(string id, string userId)
        {
            int claimed = 0;
            try
            {
                await DoOperation("claim", id, async () =>
                {
                    using (var connection = await OpenConnection())
                    {
                        using (NpgsqlCommand cmd = connection.CreateCommand())
                        {
                            cmd.CommandText = @"
                            UPDATE invitations
                            SET
                                claimed = current_timestamp,
                                claimed_id = @userId 
                            WHERE id = @id
                              AND claimed IS NULL
                              AND claimed_id IS NULL
                        ";
                            cmd.Parameters.AddWithValue("id", NpgsqlDbType.Varchar, id);
                            cmd.Parameters.AddWithValue("userId", NpgsqlDbType.Varchar, userId);
                            cmd.Prepare();
                            claimed = await cmd.ExecuteNonQueryAsync();
                        }
                    }
                    return null;
                });
            }
            catch (Exception) { } 
            return claimed != 0;
        }


    }
}
