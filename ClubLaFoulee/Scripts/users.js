$(document).ready(function () {
    $('.table').DataTable({
        "language": {
            "url": "//cdn.datatables.net/plug-ins/1.10.15/i18n/French.json"
        },
        "fnRowCallback": function (nRow, aData, iDisplayIndex) {
            $('td:eq(2)', nRow).html('<a href="mailto:' + aData[2] + '">' +
                aData[2] + '</a>');
            return nRow;
        }
    });

});



