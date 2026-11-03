
// Chart colors - Chart.js default palette in hex format
const chartColors = ['#FF6384', '#FF9F40', '#FFCD56', '#4BC0C0', '#36A2EB', '#9966FF', '#C9CBCF'];

// Status-specific colors for order status chart
const statusColorMap = {
    'Pending': '#FFCD56',      // Yellow
    'Approved': '#4BC0C0',     // Teal
    'Processing': '#36A2EB',   // Blue
    'Shipped': '#9966FF',      // Purple
    'Cancelled': '#FF6384',    // Pink/Red
    'Refunded': '#FF9F40',     // Orange
    'Unknown': '#C9CBCF'       // Gray
};

 

$.getJSON('/Admin/Dashboard/GetChartData', function (data) {

    new Chart(document.getElementById('revenueChart'), {
        type: 'line',

        data: {
            labels: data.monthlyRevenue.map(r => r.label),

            datasets: [{
                label: 'Revenue',
                data: data.monthlyRevenue.map(r => r.revenue),
                borderWidth: 1,
                borderColor: chartColors[3],
                backgroundColor: chartColors[3] + '20',
                fill: null, 
                tension: 0.4,
                pointRadius: 3,
                pointHoverRadius: 6,
                pointBackgroundColor: chartColors[3]
            }]
        },

        options: {
            scales: {
                y: {
                    beginAtZero: true
                }
            }
        }
    });


    new Chart(document.getElementById('ordersChart'), {
        type: 'bar',

        data: {
            labels: data.monthlyOrders.map(r => r.label),

            datasets: [{
                label: 'Order By Month',
                data: data.monthlyOrders.map(r => r.count),
                borderWidth: 1,
                backgroundColor: chartColors[0] + '80',  // Semi-transparent color for better visibility
                borderColor: chartColors[0],
                borderRadius: 8,  // Rounded corners for bars
                hoverBackgroundColor: chartColors[0] 
            }]
        },

        options: {
            responsive: true,
            maintainAspectRatio: false,
            Plugins: {
                legand: { display: false },
                tooltip: {
                    backgroundColor: 'rgba(0, 0, 0, 0.8)',
                    padding: 12,
                    cornerRaduis: 8,
                    displayColors: false
                }
            },
            scales: {
                y: {
                    beginAtZero: true,
                    grid: {
                        color: 'rgba(0, 0, 0, 0.5)',
                        drawBorder: false
                    },
                    ticks: {
                        stepSize: 1,
                        padding: 10,
                    }
                }, 
                x: {
                    grid: { display: false },
                    ticks: {padding: 10}
                }
            }
        }
               
    });

    new Chart(document.getElementById('statusChart'), {
        type: 'doughnut',

        data: {
            labels: data.statusBreakdown.map(r => r.status),

            datasets: [{
                label: 'Order Status',
                data: data.statusBreakdown.map(r => r.count),
                backgroundColor: data.statusBreakdown.map(r => statusColorMap[r.status] || '#999'),
                borderWidth: 2,
                borderColor: '#fff',
                hoverBorderWidth: 3,
                hoverOffSet: 10 
            }]
        },

        options: {
            responsive: true,
            maintainAspectRatio: false,
            cutout: '70%',
            Plugins: {
                legand: {
                    Position: 'bottom',
                    label: {
                        padding: 20,
                        usePointStyle: true,
                        pointStyle: 'circle',
                        font: {size:12}
                    }
                },
                tooltip: {
                    backgroundColor: 'rgba(0, 0, 0, 0.8)',
                    padding: 12,
                    cornerRaduis: 8,
                }
            },
            scales: {
                y: {
                    beginAtZero: true,
                    grid: {
                        color: 'rgba(0, 0, 0, 0.5)',
                        drawBorder: false
                    },
                    ticks: {
                        stepSize: 1,
                        padding: 10,
                    }
                },
                x: {
                    grid: { display: false },
                    ticks: { padding: 10 }
                }
            }
        }
    });

    new Chart(document.getElementById('categoryChart'), {
        type: 'bar',

        data: {
            labels: data.productsPerCategory.map(r => r.category),

            datasets: [{
                label: 'Products by Category',
                data: data.productsPerCategory.map(r => r.count),
                borderWidth: 1,
                backgroundColor: chartColors[0] + '80',  // Semi-transparent color for better visibility
                borderColor: chartColors[0],
                borderRadius: 8,  // Rounded corners for bars
                hoverBackgroundColor: chartColors[0] 
            }]
        },

        options: {
            indexAxis: 'y',
            responsive: true,
            maintainAspectRatio: false
        }
    });

});