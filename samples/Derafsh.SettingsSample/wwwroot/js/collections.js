(() => {
    const templates = {
        Contacts: '<select data-field="Type"><option>Phone</option><option>Email</option><option>Other</option></select><input data-field="Value" placeholder="Contact value"><button type="button" class="danger" data-remove>Remove</button>',
        ShippingZones: '<input data-field="CountryCode" placeholder="NL" maxlength="2"><input data-field="Fee" type="number" step="0.01" min="0" placeholder="5.00"><button type="button" class="danger" data-remove>Remove</button>',
        NotificationRecipients: '<input data-field="Email" type="email" placeholder="ops@example.com"><button type="button" class="danger" data-remove>Remove</button>'
    };

    function renumber(section) {
        const collection = section.dataset.collection;
        section.querySelectorAll('[data-row]').forEach((row, index) => {
            row.querySelectorAll('[data-field]').forEach(input => {
                const field = input.dataset.field;
                input.name = `Settings.${collection}[${index}].${field}`;
                input.id = `Settings_${collection}_${index}__${field}`;
            });
        });
    }

    document.querySelectorAll('.collection').forEach(section => {
        section.addEventListener('click', event => {
            const add = event.target.closest('[data-add]');
            const remove = event.target.closest('[data-remove]');

            if (add) {
                const row = document.createElement('div');
                row.className = 'row';
                row.dataset.row = '';
                row.innerHTML = `<input type="hidden" data-field="Id" value="0"><input type="hidden" data-field="StoreSettingsId" value="0">${templates[section.dataset.collection]}`;
                section.querySelector('[data-rows]').appendChild(row);
                renumber(section);
            }

            if (remove) {
                remove.closest('[data-row]').remove();
                renumber(section);
            }
        });
    });
})();
