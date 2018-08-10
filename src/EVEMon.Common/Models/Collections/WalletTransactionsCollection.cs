using System.Collections.Generic;
using EVEMon.Common.Collections;
using EVEMon.Common.Enumerations.CCPAPI;
using EVEMon.Common.Serialization.Eve;
using EVEMon.Common.Service;

namespace EVEMon.Common.Models.Collections
{
    public sealed class WalletTransactionsCollection : ReadonlyCollection<WalletTransaction>
    {
        private readonly CCPCharacter m_character;

        /// <summary>
        /// Internal constructor.
        /// </summary>
        /// <param name="character">The character.</param>
        internal WalletTransactionsCollection(CCPCharacter character)
        {
            m_character = character;
        }

        /// <summary>
        /// Imports an enumeration of API objects.
        /// </summary>
        /// <param name="src">The enumeration of serializable wallet transactions from the API.</param>
        internal void Import(IEnumerable<SerializableWalletTransactionsListItem> src)
        {
            Items.Clear();

            // Import the wallet transactions from the API
            foreach (SerializableWalletTransactionsListItem srcWalletTransaction in src)
            {
                Items.Add(new WalletTransaction(srcWalletTransaction, m_character));
            }
        }

        /// <summary>
        /// Imports the WalletTransactions from a cached file.
        /// </summary>
        internal void ImportFromCacheFile()
        {
            var result = LocalXmlCache.Load<SerializableAPIWalletTransactions>(m_character.Name + "-" +
                ESIAPICharacterMethods.WalletTransactions);
            if (result != null)
                Import(result.WalletTransactions);
        }

        /// <summary>
        /// Exports the WalletTransactions to the cached file.
        /// </summary>
        internal void ExportToCacheFile()
        {
            // Save the file to the cache
            string filename = m_character.Name + "-" + ESIAPICharacterMethods.WalletTransactions;
            var exported = new SerializableAPIWalletTransactions();

            foreach (WalletTransaction tx in Items)
                exported.WalletTransactions.Add(tx.Export());

            LocalXmlCache.SaveAsync(filename, Util.SerializeToXmlDocument(exported)).
                ConfigureAwait(false);

            // TODO Fire event to update the UI ?
            // EveMonClient.OnCharacterKillLogUpdated(m_ccpCharacter);
        }
    }
}
