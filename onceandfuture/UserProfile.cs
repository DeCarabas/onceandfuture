namespace OnceAndFuture
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Threading.Tasks;
    using Newtonsoft.Json;

    public class RiverDefinition
    {
        public RiverDefinition(string name, string id, IEnumerable<Uri> feeds = null)
        {
            Name = name;
            Id = id;
            Feeds = ImmutableList.CreateRange(feeds ?? Enumerable.Empty<Uri>());
        }

        public RiverDefinition With(string name = null, string id = null, IEnumerable<Uri> feeds = null)
        {
            return new RiverDefinition(name ?? Name, id ?? Id, feeds ?? Feeds);
        }

        [JsonProperty("id")]
        public string Id { get; }
        [JsonProperty("name")]
        public string Name { get; }
        [JsonProperty("feeds")]
        public ImmutableList<Uri> Feeds { get; }
    }

    public class LoginCookie
    {
        public LoginCookie(string id, DateTimeOffset expireAt)
        {
            Id = id;
            ExpireAt = expireAt;
        }

        [JsonProperty("id")]
        public string Id { get; }
        [JsonProperty("expireAt")]
        public DateTimeOffset ExpireAt { get; }
    }

    public class UserProfile
    {
        public UserProfile(
            string user = null,
            IEnumerable<RiverDefinition> rivers = null,
            IEnumerable<LoginCookie> logins = null,
            string password = null,
            string email = null,
            bool emailVerified = false)
        {
            User = user;
            Rivers = ImmutableList.CreateRange(rivers ?? Enumerable.Empty<RiverDefinition>());
            Logins = ImmutableList.CreateRange(logins ?? Enumerable.Empty<LoginCookie>());
            Password = password;
            Email = email;
            EmailVerified = emailVerified;
        }

        public UserProfile With(
            string user = null,
            IEnumerable<RiverDefinition> rivers = null,
            IEnumerable<LoginCookie> logins = null,
            string password = null,
            string email = null,
            bool? emailVerified = null)
        {
            return new UserProfile(
                user ?? User,
                rivers ?? Rivers, 
                logins ?? Logins, 
                password ?? Password, 
                email ?? Email, 
                emailVerified ?? EmailVerified);
        }

        [JsonIgnore]
        public string User { get; }
        [JsonProperty("rivers")]
        public ImmutableList<RiverDefinition> Rivers { get; }
        [JsonProperty("logins")]
        public ImmutableList<LoginCookie> Logins { get; }
        [JsonProperty("password")]
        public string Password { get; }
        [JsonProperty("email")]
        public string Email { get; }
        [JsonProperty("emailVerified")]
        public bool EmailVerified { get; }
    }


    public class UserProfileStore : DocumentStore<string, UserProfile>
    {
        public UserProfileStore() : base(new BlobStore("onceandfuture-profiles", "profiles"), "profiles") { }

        protected override UserProfile GetDefaultValue(string id) => new UserProfile();
        protected override string GetObjectID(string id) => Util.HashString(id);
        public Task<UserProfile> GetProfileFor(string user) 
            => GetDocument(user).ContinueWith(t => t.Result.With(user: user));
        public Task SaveProfile(UserProfile profile) => WriteDocument(profile.User, profile);
        public Task SaveProfileFor(string user, UserProfile profile) => WriteDocument(user, profile);
        public Task<bool> ProfileExists(string user) => DocumentExists(user);
    }
}