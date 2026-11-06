var dataTable;
$(document).ready(function () {
    var url = window.location.search;
    if (url.includes("cancelled")) { loadDataTable("cancelled"); }
    else if (url.includes("shipped")) { loadDataTable("shipped"); }
    else if (url.includes("pending")) { loadDataTable("pending"); }
    else if (url.includes("processing")) { loadDataTable("processing"); }
    else if (url.includes("approved")) { loadDataTable("approved"); }
    else { loadDataTable("all"); }
});

function statusPill(s) {
    s = s || 'Unknown';
    return '<span class="pill pill-' + s.toLowerCase() + '">' + s + '</span>';
}

function loadDataTable(status) {
    dataTable = $('#tbldata').DataTable({
        ajax: '/admin/order/Getall?status=' + status,
        order: [[0, 'desc']],
        language: { search: '', searchPlaceholder: 'Search orders…' },
        columns: [
            { data: 'id', "width": "6%", "render": function (d) { return '<span class="fw-bold">#' + d + '</span>'; } },
            { data: 'name', "width": "20%" },
            { data: 'phoneNumber', "width": "14%" },
            { data: 'applicationUser.email', "width": "22%", "defaultContent": '' },
            { data: 'orderStatus', "width": "14%", "render": function (data) { return statusPill(data); } },
            { data: 'orderTotal', "width": "12%", "render": function (data) { return '<span class="fw-bold">$' + Number(data).toFixed(2) + '</span>'; } },
            {
                data: 'id', "width": "12%", "orderable": false, "render": function (data) {
                    return '<div class="d-flex gap-2 justify-content-end"><a href="/admin/order/details?orderId=' + data + '" class="btn btn-sm btn-soft"><i class="bi bi-arrow-up-right me-1"></i>Details</a></div>';
                }
            }
        ]
    });
}
