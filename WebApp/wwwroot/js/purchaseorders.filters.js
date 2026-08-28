function setActivePurchaseOrderFilter(buttonId) {
	var filterButtons = ['btnPartiallyDelivered', 'btnDelivered', 'btnCancelled', 'btnApproved', 'btnClear'];
	for (var i = 0; i < filterButtons.length; i++) {
		var button = document.getElementById(filterButtons[i]);
		if (button) {
			button.classList.toggle('active', filterButtons[i] === buttonId);
		}
	}
}

function filterBySearchString() {
	var input, filter, table, tr, tdOrderNumber;
	input = document.getElementById('searchOrders');
	filter = input.value.toUpperCase();
	table = document.getElementById('ordersTable');
	tr = table.getElementsByTagName('tr');
	for (i = 0; i < tr.length; i++) {
		tdOrderNumber = tr[i].getElementsByTagName('td')[0];
		tdSupplierName = tr[i].getElementsByTagName('td')[1];
		if (tdOrderNumber && tdSupplierName) {
			textValueOrderNumber = tdOrderNumber.textContent || tdOrderNumber.innerText;
			textValueSupplierName = tdSupplierName.textContent || tdSupplierName.innerText;
			if (textValueOrderNumber.toUpperCase().indexOf(filter) > -1 || textValueSupplierName.toUpperCase().indexOf(filter) > -1) {
				tr[i].style.display = '';
			} else {
				tr[i].style.display = 'none';
			};
		};
	};
	setActivePurchaseOrderFilter('');
};

function filterByPartiallyDelivered() {
	var table, tr, i, orderStatus, spanStatus, textSpanStatus, filter, btnPartiallyDelivered;
	setActivePurchaseOrderFilter('btnPartiallyDelivered');
	filter = localizedData.statusPartiallyDelivered.toUpperCase();
	table = document.getElementById('ordersTable');
	tr = table.getElementsByTagName('tr');
	for (i = 0; i < tr.length; i++) {
		orderStatus = tr[i].getElementsByTagName('td')[6];
		if (orderStatus) {
			spanStatus = orderStatus.getElementsByTagName('span')[0];
			if (spanStatus) {
				textSpanStatus = spanStatus.textContent || spanStatus.innerText;
				if (textSpanStatus) {
					if (textSpanStatus.toUpperCase().indexOf(filter) > -1) {
						tr[i].style.display = '';
					} else {
						tr[i].style.display = 'none';
					};
				}
			};
		};
	};
};

function filterByDelivered() {
	var table, tr, i, orderStatus, spanStatus, textSpanStatus, filter;
	setActivePurchaseOrderFilter('btnDelivered');
	filter = localizedData.statusDelivered.toUpperCase();
	table = document.getElementById('ordersTable');
	tr = table.getElementsByTagName('tr');
	for (i = 0; i < tr.length; i++) {
		orderStatus = tr[i].getElementsByTagName('td')[6];
		if (orderStatus) {
			spanStatus = orderStatus.getElementsByTagName('span')[0];
			if (spanStatus) {
				textSpanStatus = spanStatus.textContent || spanStatus.innerText;
				if (textSpanStatus) {
					//if (textSpanStatus.toUpperCase().indexOf(filter) > -1) {
					if (textSpanStatus.toUpperCase() == filter) {
						tr[i].style.display = '';
					} else {
						tr[i].style.display = 'none';
					};
				}
			};
		};
	};
};

function filterByCancelled() {
	var table, tr, i, orderStatus, spanStatus, textSpanStatus, filter;
	setActivePurchaseOrderFilter('btnCancelled');
	filter = localizedData.statusCancelled.toUpperCase();
	table = document.getElementById('ordersTable');
	tr = table.getElementsByTagName('tr');
	for (i = 0; i < tr.length; i++) {
		console.log(tr[i]);
		orderStatus = tr[i].getElementsByTagName('td')[6];
		if (orderStatus) {
			spanStatus = orderStatus.getElementsByTagName('span')[0];
			if (spanStatus) {
				textSpanStatus = spanStatus.textContent || spanStatus.innerText;
				if (textSpanStatus) {
					if (textSpanStatus.toUpperCase() == filter) {
						tr[i].style.display = '';
					} else {
						tr[i].style.display = 'none';
					};
				}
			};
		};
	};
};

function filterByApproved() {
	var table, tr, i, orderStatus, spanStatus, textSpanStatus, filter;
	setActivePurchaseOrderFilter('btnApproved');
	filter = localizedData.statusApproved.toUpperCase();
	table = document.getElementById('ordersTable');
	tr = table.getElementsByTagName('tr');
	for (i = 0; i < tr.length; i++) {
		console.log(tr[i]);
		orderStatus = tr[i].getElementsByTagName('td')[6];
		if (orderStatus) {
			spanStatus = orderStatus.getElementsByTagName('span')[0];
			if (spanStatus) {
				textSpanStatus = spanStatus.textContent || spanStatus.innerText;
				if (textSpanStatus) {
					if (textSpanStatus.toUpperCase() == filter) {
						tr[i].style.display = '';
					} else {
						tr[i].style.display = 'none';
					};
				}
			};
		};
	};
};

function filterClear() {
	var table, tr, i, btnClear;
	setActivePurchaseOrderFilter('btnClear');
	btnNotApproved = document.getElementById('btnClear');
	table = document.getElementById('ordersTable');
	tr = table.getElementsByTagName('tr');
	for (i = 0; i < tr.length; i++) {
		tr[i].style.display = '';
	};
};
