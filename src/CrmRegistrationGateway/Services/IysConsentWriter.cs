using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using RelatedEntegrasyonu.CrmGateway.Models;

namespace RelatedEntegrasyonu.CrmGateway.Services
{
    /// <summary>
    /// Web formundaki kanal onaylarını CRM'deki mevcut İYS eklentileri üzerinden işler.
    ///
    /// Sıra (TEST CRM'de doğrulandı, 2026-10-05):
    /// 1. Lead Create → IYSKontrol.MusteriAdayiCreate (senkron) e-posta ve telephone1 için
    ///    İYS kaydını bulur ya da "Boş" durumla açar ve Lead'e bağlar.
    /// 2. Bu sınıf Lead'de altium_emailonayi / altium_mesajonayi / altium_aramaonayi
    ///    bayraklarını true yapar → ManuelMkvyOnay eklentileri İYS kaydını "Onaylı" yapar,
    ///    İYS aktivitesi oluşturur ve bayrağı tekrar false'a çeker.
    /// 3. Eklenti aktiviteyi "Manuel" kaynakla açtığı için bu sınıf kaynağı "Webform" yapar.
    ///
    /// İşaretlenmeyen kanallara dokunulmaz (ret yazılmaz, mevcut durum korunur).
    /// Lead için o kanalda zaten İYS aktivitesi varsa bayrak tekrar gönderilmez.
    /// </summary>
    internal static class IysConsentWriter
    {
        internal const string EmailFlag = "altium_emailonayi";
        internal const string SmsFlag = "altium_mesajonayi";
        internal const string CallFlag = "altium_aramaonayi";

        internal const string ActivityEntity = "altium_iysaktivitesi";
        internal const string ActivityChannel = "altium_iystipii";
        internal const string ActivitySource = "altium_onaykaynagi";
        internal const int ChannelEmail = 1;
        internal const int ChannelCall = 2;
        internal const int ChannelSms = 3;
        internal const int SourceWebform = 1;
        internal const int SourceManual = 2;

        public static void Apply(IOrganizationService service, string leadEntity, Guid leadId, CrmRegistrationRequest request, string iysPhone)
        {
            var wanted = new Dictionary<int, string>();
            if (request.EmailConsent && !string.IsNullOrWhiteSpace(request.Email))
                wanted[ChannelEmail] = EmailFlag;
            // The CRM plugins only create phone İYS records for Turkish numbers.
            bool turkishPhone = CrmRegistrationWriter.NormalizeTurkishPhone(iysPhone) != null;
            if (request.SmsConsent && turkishPhone)
                wanted[ChannelSms] = SmsFlag;
            if (request.CallConsent && turkishPhone)
                wanted[ChannelCall] = CallFlag;
            if (wanted.Count == 0)
                return;

            foreach (int channel in ExistingChannels(service, leadId))
                wanted.Remove(channel);
            if (wanted.Count == 0)
                return;

            var update = new Entity(leadEntity, leadId);
            foreach (string flag in wanted.Values)
                update[flag] = true;
            service.Update(update);

            MarkAsWebform(service, leadId, wanted.Keys);
        }

        private static IEnumerable<int> ExistingChannels(IOrganizationService service, Guid leadId)
        {
            var query = new QueryExpression(ActivityEntity) { ColumnSet = new ColumnSet(ActivityChannel) };
            query.Criteria.AddCondition("regardingobjectid", ConditionOperator.Equal, leadId);
            return service.RetrieveMultiple(query).Entities
                .Where(e => e.Contains(ActivityChannel))
                .Select(e => e.GetAttributeValue<OptionSetValue>(ActivityChannel).Value)
                .ToList();
        }

        // The plugin closes the activity it creates and closed activities cannot be edited,
        // so a closed activity is reopened, corrected and returned to its original state.
        private static void MarkAsWebform(IOrganizationService service, Guid leadId, IEnumerable<int> channels)
        {
            var query = new QueryExpression(ActivityEntity) { ColumnSet = new ColumnSet("statecode", "statuscode") };
            query.Criteria.AddCondition("regardingobjectid", ConditionOperator.Equal, leadId);
            query.Criteria.AddCondition(ActivitySource, ConditionOperator.Equal, SourceManual);
            query.Criteria.AddCondition(ActivityChannel, ConditionOperator.In, channels.Cast<object>().ToArray());
            foreach (Entity activity in service.RetrieveMultiple(query).Entities)
            {
                var state = activity.GetAttributeValue<OptionSetValue>("statecode");
                var status = activity.GetAttributeValue<OptionSetValue>("statuscode");
                bool closed = state != null && state.Value != 0;
                if (closed)
                    SetState(service, activity.Id, 0, 1);

                var update = new Entity(ActivityEntity, activity.Id);
                update[ActivitySource] = new OptionSetValue(SourceWebform);
                service.Update(update);

                if (closed)
                    SetState(service, activity.Id, state.Value, status != null ? status.Value : -1);
            }
        }

        private static void SetState(IOrganizationService service, Guid activityId, int state, int status)
        {
            var change = new Entity(ActivityEntity, activityId);
            change["statecode"] = new OptionSetValue(state);
            change["statuscode"] = new OptionSetValue(status);
            service.Update(change);
        }
    }
}
