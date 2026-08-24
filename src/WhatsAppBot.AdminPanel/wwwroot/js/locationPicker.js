window.locationPicker = {
    maps: {},

    init: function (elementId, lat, lng, dotNetRef) {
        // Si ya había un mapa en este elemento (ej. el usuario volvió a esta
        // página en la misma sesión de Blazor Server), lo destruimos antes
        // de crear uno nuevo — Leaflet no permite re-inicializar el mismo div.
        this.dispose(elementId);

        const map = L.map(elementId).setView([lat, lng], 15);

        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>',
            maxZoom: 19
        }).addTo(map);

        const marker = L.marker([lat, lng], { draggable: true }).addTo(map);

        const notify = (latlng) => dotNetRef.invokeMethodAsync('OnLocationPicked', latlng.lat, latlng.lng);

        marker.on('dragend', (e) => notify(e.target.getLatLng()));
        map.on('click', (e) => {
            marker.setLatLng(e.latlng);
            notify(e.latlng);
        });

        this.maps[elementId] = { map, marker };
    },

    setPosition: function (elementId, lat, lng) {
        const entry = this.maps[elementId];
        if (!entry) return;
        entry.marker.setLatLng([lat, lng]);
        entry.map.setView([lat, lng], 16);
    },

    dispose: function (elementId) {
        const entry = this.maps[elementId];
        if (entry) {
            entry.map.remove();
            delete this.maps[elementId];
        }
    }
};
