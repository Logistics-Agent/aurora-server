using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MimeKit;
using MailService.Application.Interfaces.Storage;
using MailService.Domain.Entities;
using MailService.Domain.Enums;

namespace MailService.Application.Common;

public static class MimeMessageHelper
{
    public static string ReplaceCidWithDataUris(string html, MimeMessage message)
    {
        if (string.IsNullOrWhiteSpace(html)) return html;

        foreach (var entity in message.BodyParts)
        {
            if (entity is MimePart part && part.Content != null)
            {
                string? cid = part.ContentId?.Trim('<', '>', ' ');
                string? filename = part.FileName ?? part.ContentType?.Name;
                string mimeType = part.ContentType?.MimeType ?? "image/png";

                try
                {
                    using var mem = new MemoryStream();
                    part.Content.DecodeTo(mem);
                    byte[] bytes = mem.ToArray();
                    if (bytes.Length > 0)
                    {
                        string base64 = Convert.ToBase64String(bytes);
                        string dataUri = $"data:{mimeType};base64,{base64}";

                        if (!string.IsNullOrEmpty(cid))
                        {
                            html = html.Replace($"cid:{cid}", dataUri, StringComparison.OrdinalIgnoreCase);
                        }
                        if (!string.IsNullOrEmpty(filename))
                        {
                            html = html.Replace($"cid:{filename}", dataUri, StringComparison.OrdinalIgnoreCase);
                        }
                    }
                }
                catch { }
            }
        }
        return html;
    }

    public static async Task<List<MessageAttachmentMeta>> ExtractAndUploadAttachmentsAsync(
        MimeMessage message,
        Guid tenantId,
        string rfcMessageId,
        IR2StorageClient? storageClient,
        CancellationToken cancellationToken = default)
    {
        var attachmentsMeta = new List<MessageAttachmentMeta>();
        var processedParts = new HashSet<MimeEntity>();

        foreach (var entity in message.BodyParts)
        {
            if (entity is TextPart textPart && !textPart.IsAttachment && string.IsNullOrEmpty(textPart.FileName) && (textPart.IsPlain || textPart.IsHtml))
                continue;

            if (entity is MimePart part)
            {
                string? filename = part.FileName 
                                ?? part.ContentDisposition?.FileName 
                                ?? part.ContentType?.Name;

                if (!string.IsNullOrEmpty(filename) || part.IsAttachment)
                {
                    processedParts.Add(part);
                    long size = 0;
                    string? presignedUrl = null;
                    if (part.Content != null)
                    {
                        try
                        {
                            using var mem = new MemoryStream();
                            part.Content.DecodeTo(mem);
                            byte[] contentBytes = mem.ToArray();
                            size = contentBytes.Length;

                            if (storageClient != null && contentBytes.Length > 0)
                            {
                                string safeName = filename ?? "attachment";
                                string r2Key = await storageClient.UploadAttachmentAsync(
                                    tenantId,
                                    rfcMessageId,
                                    EmailDirection.Inbound,
                                    safeName,
                                    contentBytes,
                                    cancellationToken);

                                presignedUrl = await storageClient.GeneratePresignedUrlAsync(r2Key, 86400, cancellationToken);
                            }
                        }
                        catch { }
                    }

                    attachmentsMeta.Add(new MessageAttachmentMeta
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        FileName = filename ?? "attachment",
                        ContentType = part.ContentType?.MimeType ?? "application/octet-stream",
                        SizeBytes = size,
                        Url = presignedUrl
                    });
                }
            }
        }

        foreach (var entity in message.Attachments)
        {
            if (processedParts.Contains(entity))
                continue;

            if (entity is MimePart part)
            {
                string filename = part.FileName 
                               ?? part.ContentDisposition?.FileName 
                               ?? part.ContentType?.Name 
                               ?? "attachment";
                long size = 0;
                string? presignedUrl = null;
                if (part.Content != null)
                {
                    try
                    {
                        using var mem = new MemoryStream();
                        part.Content.DecodeTo(mem);
                        byte[] contentBytes = mem.ToArray();
                        size = contentBytes.Length;

                        if (storageClient != null && contentBytes.Length > 0)
                        {
                            string r2Key = await storageClient.UploadAttachmentAsync(
                                tenantId,
                                rfcMessageId,
                                EmailDirection.Inbound,
                                filename,
                                contentBytes,
                                cancellationToken);

                            presignedUrl = await storageClient.GeneratePresignedUrlAsync(r2Key, 86400, cancellationToken);
                        }
                    }
                    catch { }
                }

                attachmentsMeta.Add(new MessageAttachmentMeta
                {
                    Id = Guid.NewGuid().ToString("N"),
                    FileName = filename,
                    ContentType = part.ContentType?.MimeType ?? "application/octet-stream",
                    SizeBytes = size,
                    Url = presignedUrl
                });
            }
        }

        return attachmentsMeta;
    }
}
