var userDataTable;

$(document).ready(function () {
    LoadDataTable();
});

function LoadDataTable() {

    userDataTable = $('#tblData').DataTable({
        ajax: '/admin/user/Getall',

        columns: [
            {
                data: 'name',
                width: '25%'
            },
            {
                data: 'email',
                width: '15%'
            },
            {
                data: 'phoneNumber',
                width: '15%'
            },
            {
                data: 'role',
                width: '10%',
                render: function (data) {
                    return '<span class="badge bg-secondary">' + data + '</span>';
                }
            },
            {
                data: null,
                width: '15%',

                render: function (data, type, row) {

                    var today = new Date().getTime();

                    var lockout = row.lockoutEnd
                        ? new Date(row.lockoutEnd).getTime()
                        : 0;

                    var isLocked = lockout > today;

                    return `
                        <div class="d-flex gap-2 justify-content-end">

                            <a onclick="LockUnlock('${row.id}')" 
                               class="btn btn-sm ${isLocked ? 'btn-outline-success' : 'btn-outline-danger'}">
                                <i class="bi bi-${isLocked ? 'unlock' : 'lock'}-fill"></i>
                                ${isLocked ? 'Unlock' : 'Lock'}
                            </a>

                            <a href="/admin/user/Role?id=${row.id}" 
                               class="btn btn-sm btn-outline-secondary">
                                <i class="bi bi-pencil-square"></i>
                                Role
                            </a>

                            <a href="/admin/user/ResetPassword?id=${row.id}" 
                               class="btn btn-sm btn-outline-warning">
                                <i class="bi bi-key"></i>
                                Password
                            </a>

                        </div>
                    `;
                }
            }
        ]
    });
}

function LockUnlock(id) {

    $.ajax({
        type: 'POST',
        url: '/admin/user/LockUnlock',
        data: JSON.stringify(id),
        contentType: 'application/json',

        success: function (data) {

            if (data.success) {

                toastr.success(data.message);

                userDataTable.ajax.reload();
            }
            else {
                toastr.error(data.message);
            }
        },

        error: function () {
            toastr.error("Something went wrong.");
        }
    });
}