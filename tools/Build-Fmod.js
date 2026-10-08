// Executed only against .local/fmod/DohnaDohna.fspro by fmodstudiocl.
var bank = studio.project.lookup('bank:/DohnaDohna');
if (!bank) {
    bank = studio.project.create('Bank');
    bank.name = 'DohnaDohna';
    bank.folder = studio.project.workspace.masterBankFolder;
}
var folder = studio.project.lookup('event:/DohnaDohna');
if (!folder) {
    folder = studio.project.create('EventFolder');
    folder.name = 'DohnaDohna';
    folder.folder = studio.project.workspace.masterEventFolder;
}
audioRows.forEach(function(row) {
    var existing = studio.project.lookup(row.event);
    var asset = studio.project.importAudioFile(row.asset);
    if (!asset) throw new Error('Audio import failed: ' + row.asset);
    var event = existing || studio.project.create('Event');
    event.name = row.event.split('/').pop();
    event.folder = folder;
    if (event.banks.indexOf(bank) < 0) event.relationships.banks.add(bank);
    event.mixer.masterBus.output = studio.project.lookup('bus:/master/sfx');
    if (event.groupTracks.length) return;
    var track = event.addGroupTrack('Source');
    var sound = track.addSound(event.timeline, 'SingleSound', 0, asset.length);
    sound.audioFile = asset;
});
studio.project.save();
