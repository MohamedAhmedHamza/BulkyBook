var productDataTable;

$(document).ready(function () {
    loadDataTable();
})
function loadDataTable() {
    productDataTable =
        $('#tbldata').DataTable({
            ajax: '/admin/Product/Getall',
            columns: [
                { data: 'title', "width": "25%" },
                { data: 'isbn', "width": "15%" },
                { data: 'price', "width": "15%", "render": function (data, type, row) { return '$' + data.toFixed(2); } },
                { data: 'author', "width": "15%" },
                {
                    data: 'category.name', "width": "10%", "render": function (data) { return '<span class= "badge bg-secondary">' + data + '</span>'; }
                },
                {
                    data: 'id', "width": "15%", "render": function (data) {
                        return `
                    <div class="d-flex gap-2 justify-content-end">
                    <a href="/admin/product/upsert?id=${data}" class="btn btn-sm btn-outline-success">
                    <i class="bi bi-pencil-square">Edit</i> 
                    </a>
                    <a onclick="Delete('/admin/product/delete/${data}')" class="btn btn-sm btn-outline-danger">
                    <i class="bi bi-trash">Delete</i> 
                    </a>
                    </div>`;
                    }
                }
            ]
        });
}
function Delete(url) {
    Swal.fire({
        title: "Are you sure?",
        text: "You won't be able to revert this!",
        icon: "warning",
        showCancelButton: true,
        confirmButtonColor: "#3085d6",
        cancelButtonColor: "#d33",
        confirmButtonText: "Yes, delete it!"
    }).then((result) => {
        if (result.isConfirmed) {

            $.ajax({
                url: url,
                type: 'DELETE',
                success: function (data) {
                    productDataTable.ajax.reload();
                    Swal.fire({
                        title: "Deleted!",
                        text: "Your file has been deleted.",
                        icon: "success"
                    });
                }
            })
        }
            
        
    });
}
