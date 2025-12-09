using System.Buffers;
using System.Text.Json;

namespace xbytechat.api.Features.Webhooks.Pinnacle.Services.Adapters;

public sealed class PinnacleToMetaAdapter : IPinnacleToMetaAdapter
{
    public JsonElement ToMetaEnvelope(JsonElement p)
    {
        var buf = new ArrayBufferWriter<byte>();
        using var w = new Utf8JsonWriter(buf);

        w.WriteStartObject();
        w.WritePropertyName("entry");
        w.WriteStartArray();
        w.WriteStartObject(); // entry[0]
        w.WritePropertyName("changes");
        w.WriteStartArray();
        w.WriteStartObject(); // changes[0]
        w.WritePropertyName("value");
        w.WriteStartObject();

        // NEW: try to emit metadata up-front (harmless if not found)
        WriteMetadata(p, w);

        if (!TryMapStatuses(p, w) && !TryMapMessages(p, w))
        {
            w.WritePropertyName("provider_raw");
            p.WriteTo(w);
        }

        w.WriteEndObject(); // value
        w.WriteEndObject(); // change
        w.WriteEndArray();  // changes
        w.WriteEndObject(); // entry[0]
        w.WriteEndArray();  // entry
        w.WriteEndObject(); // root
        w.Flush();

        var ros = new ReadOnlySequence<byte>(buf.WrittenMemory);
        using var doc = JsonDocument.Parse(ros);
        return doc.RootElement.Clone();
    }

    // ---- NEW ----
    // Best-effort extraction; tolerate any Pinnacle layout you have.
    // We only write fields if we can resolve them.
    private static void WriteMetadata(JsonElement p, Utf8JsonWriter w)
    {
        string? displayPhone = null;
        string? phoneNumberId = null;
        string? wabaId = null;

        // common guesses; add/rename to match your Pinnacle payload
        // 1) flat
        if (p.TryGetProperty("display_phone_number", out var d1) && d1.ValueKind == JsonValueKind.String) displayPhone = d1.GetString();
        if (p.TryGetProperty("phone_number_id", out var pid1) && pid1.ValueKind == JsonValueKind.String) phoneNumberId = pid1.GetString();
        if (p.TryGetProperty("waba_id", out var wa1) && wa1.ValueKind == JsonValueKind.String) wabaId = wa1.GetString();

        // 2) channel
        if (p.TryGetProperty("channel", out var ch) && ch.ValueKind == JsonValueKind.Object)
        {
            if (displayPhone is null && ch.TryGetProperty("display_phone_number", out var d2) && d2.ValueKind == JsonValueKind.String) displayPhone = d2.GetString();
            if (displayPhone is null && ch.TryGetProperty("phone", out var d3) && d3.ValueKind == JsonValueKind.String) displayPhone = d3.GetString();

            if (phoneNumberId is null && ch.TryGetProperty("phone_number_id", out var pid2) && pid2.ValueKind == JsonValueKind.String) phoneNumberId = pid2.GetString();
            if (phoneNumberId is null && ch.TryGetProperty("id", out var pid3) && pid3.ValueKind == JsonValueKind.String) phoneNumberId = pid3.GetString();

            if (wabaId is null && ch.TryGetProperty("waba_id", out var wa2) && wa2.ValueKind == JsonValueKind.String) wabaId = wa2.GetString();
        }

        // 3) meta-style wrapper
        if (p.TryGetProperty("metadata", out var meta) && meta.ValueKind == JsonValueKind.Object)
        {
            if (displayPhone is null && meta.TryGetProperty("display_phone_number", out var d4) && d4.ValueKind == JsonValueKind.String) displayPhone = d4.GetString();
            if (phoneNumberId is null && meta.TryGetProperty("phone_number_id", out var pid4) && pid4.ValueKind == JsonValueKind.String) phoneNumberId = pid4.GetString();
            if (wabaId is null && meta.TryGetProperty("waba_id", out var wa3) && wa3.ValueKind == JsonValueKind.String) wabaId = wa3.GetString();
        }

        if (displayPhone is null && p.TryGetProperty("business", out var biz) && biz.ValueKind == JsonValueKind.Object)
        {
            if (biz.TryGetProperty("phone", out var d5) && d5.ValueKind == JsonValueKind.String) displayPhone = d5.GetString();
            if (biz.TryGetProperty("phone_id", out var pid5) && pid5.ValueKind == JsonValueKind.String) phoneNumberId = pid5.GetString();
        }

        // Only emit if we have at least one of them.
        if (displayPhone is not null || phoneNumberId is not null || wabaId is not null)
        {
            w.WritePropertyName("metadata");
            w.WriteStartObject();
            if (displayPhone is not null) w.WriteString("display_phone_number", displayPhone);
            if (phoneNumberId is not null) w.WriteString("phone_number_id", phoneNumberId);
            if (wabaId is not null) w.WriteString("waba_id", wabaId);
            w.WriteEndObject();
        }
    }
    // ---- NEW END ----

    private static bool TryMapStatuses(JsonElement p, Utf8JsonWriter w)
    {
        string? id = null, status = null;
        long? ts = null;

        if (p.TryGetProperty("messageId", out var mid) && mid.ValueKind == JsonValueKind.String) id = mid.GetString();
        if (p.TryGetProperty("id", out var pid) && pid.ValueKind == JsonValueKind.String) id ??= pid.GetString();
        if (p.TryGetProperty("status", out var st) && st.ValueKind == JsonValueKind.String) status = st.GetString();

        if (p.TryGetProperty("timestamp", out var t))
        {
            if (t.ValueKind == JsonValueKind.Number) ts = t.GetInt64();
            else if (t.ValueKind == JsonValueKind.String && long.TryParse(t.GetString(), out var n)) ts = n;
        }

        if (p.TryGetProperty("data", out var d) && d.ValueKind == JsonValueKind.Object)
        {
            if (id is null && d.TryGetProperty("id", out var did) && did.ValueKind == JsonValueKind.String) id = did.GetString();
            if (status is null && d.TryGetProperty("status", out var ds) && ds.ValueKind == JsonValueKind.String) status = ds.GetString();
            if (ts is null && d.TryGetProperty("ts", out var dts) && dts.ValueKind == JsonValueKind.Number) ts = dts.GetInt64();
        }

        if (id is null || status is null) return false;

        w.WritePropertyName("statuses");
        w.WriteStartArray();
        w.WriteStartObject();
        w.WriteString("id", id);
        w.WriteString("status", status);
        if (ts.HasValue) w.WriteNumber("timestamp", ts.Value);
        w.WriteEndObject();
        w.WriteEndArray();
        return true;
    }

    
    private static bool TryMapMessages(JsonElement p, Utf8JsonWriter w)
    {
        // { "click": { "title":"Flow Test", "contextId":"wamid..", "from":"<biz_phone?>" , "user":"<customer_wa_id?>" } }
        if (p.TryGetProperty("click", out var c) && c.ValueKind == JsonValueKind.Object)
        {
            var title = c.TryGetProperty("title", out var t) ? t.GetString() : null;
            var ctxId = c.TryGetProperty("contextId", out var ctx) ? ctx.GetString() : null;

            // customer who clicked
            var customerFrom = c.TryGetProperty("user", out var u) && u.ValueKind == JsonValueKind.String
                ? u.GetString()
                : (c.TryGetProperty("from", out var f1) && f1.ValueKind == JsonValueKind.String ? f1.GetString() : null);

            // **business** number that sent the message (Meta provides this as context.from)
            var businessFrom =
                (p.TryGetProperty("display_phone_number", out var d1) && d1.ValueKind == JsonValueKind.String) ? d1.GetString() :
                (p.TryGetProperty("channel", out var ch) && ch.ValueKind == JsonValueKind.Object &&
                 ch.TryGetProperty("phone", out var d2) && d2.ValueKind == JsonValueKind.String) ? d2.GetString() :
                (c.TryGetProperty("from", out var f2) && f2.ValueKind == JsonValueKind.String ? f2.GetString() : null); // last resort

            if (!string.IsNullOrWhiteSpace(title) && !string.IsNullOrWhiteSpace(ctxId))
            {
                w.WritePropertyName("messages");
                w.WriteStartArray();
                w.WriteStartObject();
                w.WriteString("type", "button");
                w.WriteString("from", customerFrom ?? "");          // customer wa_id
                w.WritePropertyName("button");
                w.WriteStartObject();
                w.WriteString("text", title!);
                w.WriteEndObject();
                w.WritePropertyName("context");
                w.WriteStartObject();
                w.WriteString("id", ctxId!);
                if (!string.IsNullOrWhiteSpace(businessFrom))
                    w.WriteString("from", businessFrom!);           // **important for directory resolution**
                w.WriteEndObject();
                w.WriteEndObject();
                w.WriteEndArray();
                return true;
            }
        }

        // { "message": { "from":"<customer_wa_id>", "body":"hi", "type":"text" }, "channel":{ "phone":"<biz_phone>" } }
        if (p.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.Object)
        {
            var type = m.TryGetProperty("type", out var tp) ? tp.GetString() : "text";
            var from = m.TryGetProperty("from", out var fr) ? fr.GetString() : "";

            w.WritePropertyName("messages");
            w.WriteStartArray();
            w.WriteStartObject();
            w.WriteString("type", type ?? "text");
            w.WriteString("from", from ?? "");
            if ((type ?? "text") == "text")
            {
                var body = m.TryGetProperty("body", out var bd) ? bd.GetString() : "";
                w.WritePropertyName("text");
                w.WriteStartObject();
                w.WriteString("body", body ?? "");
                w.WriteEndObject();
            }
            w.WriteEndObject();
            w.WriteEndArray();
            return true;
        }

        return false;
    }
}
