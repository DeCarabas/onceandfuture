namespace OnceAndFuture.DAL
{
    using Npgsql;
    using NpgsqlTypes;
    using OnceAndFuture.Syndication;
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public class FeedItemsStore : DalBase
    {
        public FeedItemsStore() : base("items") { }

        public async Task<Item[]> StoreItems(Uri originUrl, Item[] items)
        {
            var stored = new List<Item>(items.Length);
            await DoOperation("storeItems", originUrl.AbsoluteUri, async () =>
            {
                using (var connection = await OpenConnection())
                {
                    using (NpgsqlCommand insertItem = connection.CreateCommand())
                    using (NpgsqlCommand insertEnclosure = connection.CreateCommand())
                    {
                        insertEnclosure.CommandText = @"
                        INSERT INTO enclosures (
                            feed_id, item_id, url, type, length
                        ) VALUES (
                            @feed_id, @item_id, @url, @type, @length
                        )
                        ON CONFLICT DO NOTHING
                        ";
                        var enclosureFeedId = insertEnclosure.Parameters.Add("@feed_id", NpgsqlDbType.Varchar);
                        var enclosureItemId = insertEnclosure.Parameters.Add("@item_id", NpgsqlDbType.Varchar);
                        var enclosureUrl = insertEnclosure.Parameters.Add("@url", NpgsqlDbType.Varchar);
                        var enclosureType = insertEnclosure.Parameters.Add("@type", NpgsqlDbType.Varchar);
                        var enclosureLength = insertEnclosure.Parameters.Add("@length", NpgsqlDbType.Varchar);
                        await insertEnclosure.PrepareAsync();

                        SetParameter(enclosureFeedId, originUrl.AbsoluteUri);

                        insertItem.CommandText = @"
                        INSERT INTO items (
                            feed_id, item_id, title, link, body, pubDate, 
                            permaLink, comments, content, description, summary,
                            thumb_url, thumb_width, thumb_height, epoch
                        ) VALUES (
                            @feed_id, @item_id, @title, @link, @body, @pubDate,
                            @permaLink, @comments, @content, @description, 
                            @summary, @thumb_url, @thumb_width, @thumb_height,
                            @epoch
                        )
                        ON CONFLICT DO NOTHING
                        ";
                        var feedIdParam = insertItem.Parameters.Add("@feed_id", NpgsqlDbType.Varchar);
                        var itemIdParam = insertItem.Parameters.Add("@item_id", NpgsqlDbType.Varchar);
                        var titleParam = insertItem.Parameters.Add("@title", NpgsqlDbType.Varchar);
                        var linkParam = insertItem.Parameters.Add("@link", NpgsqlDbType.Varchar);
                        var bodyParam = insertItem.Parameters.Add("@body", NpgsqlDbType.Varchar);
                        var pubDateParam = insertItem.Parameters.Add("@pubDate", NpgsqlDbType.TimestampTz);
                        var permaLinkParam = insertItem.Parameters.Add("@permaLink", NpgsqlDbType.Varchar);
                        var commentsParam = insertItem.Parameters.Add("@comments", NpgsqlDbType.Varchar);
                        var contentParam = insertItem.Parameters.Add("@content", NpgsqlDbType.Varchar);
                        var descriptionParam = insertItem.Parameters.Add("@description", NpgsqlDbType.Varchar);
                        var summaryParam = insertItem.Parameters.Add("@summary", NpgsqlDbType.Varchar);
                        var thumbUrlParam = insertItem.Parameters.Add("@thumb_url", NpgsqlDbType.Varchar);
                        var thumbWidthParam = insertItem.Parameters.Add("@thumb_width", NpgsqlDbType.Integer);
                        var thumbHeightParam = insertItem.Parameters.Add("@thumb_height", NpgsqlDbType.Integer);
                        var epochParam = insertItem.Parameters.Add("@epoch", NpgsqlDbType.Bigint);
                        await insertItem.PrepareAsync();

                        SetParameter(feedIdParam, originUrl.AbsoluteUri);
                        SetParameter(epochParam, DateTime.UtcNow.Ticks);
                        for (int i = 0; i < items.Length; i++)
                        {
                            Item item = items[i];
                            foreach(Enclosure enclosure in item.Enclosures)
                            {
                                SetParameter(enclosureItemId, item.Id);
                                SetParameter(enclosureUrl, enclosure.Url.AbsoluteUri);
                                SetParameter(enclosureType, enclosure.Type);
                                SetParameter(enclosureLength, enclosure.Length);
                                await insertEnclosure.ExecuteNonQueryAsync();
                            }

                            SetParameter(itemIdParam, item.Id);
                            SetParameter(titleParam, item.Title);
                            SetParameter(linkParam, item.Link?.AbsoluteUri);                            
                            SetParameter(bodyParam, item.Body);
                            SetParameter(pubDateParam, item.PubDate);
                            SetParameter(permaLinkParam, item.PermaLink?.AbsoluteUri);
                            SetParameter(commentsParam, item.Comments);
                            SetParameter(contentParam, item.Content?.ToString());
                            SetParameter(descriptionParam, item.Description?.ToString());
                            SetParameter(summaryParam, item.Summary?.ToString());
                            
                            if (item.Thumbnail != null)
                            {
                                SetParameter(thumbUrlParam, item.Thumbnail.Url.AbsoluteUri);
                                SetParameter(thumbWidthParam, item.Thumbnail.Width);
                                SetParameter(thumbHeightParam, item.Thumbnail.Height);
                            } else
                            {
                                SetParameter(thumbUrlParam, null);
                                SetParameter(thumbWidthParam, null);
                                SetParameter(thumbHeightParam, null);
                            }


                            int affected = await insertItem.ExecuteNonQueryAsync();
                            if (affected > 0)
                            {
                                stored.Add(item);
                            }
                        }
                    }
                }
                return null;
            });
            return stored.ToArray();
        }

        void SetParameter(NpgsqlParameter p, object v)
        {
            p.Value = v ?? DBNull.Value;
        }
    }
}
