function filterBySearchString() {
	var input, filter, table, tr, tdOrderNumber, tdCustomerNumber, tdCustomer, i, textValue;
	input = document.getElementById('searchOrders');
	filter = input.value.toUpperCase();
	table = document.getElementById('ordersTable');
	tr = table.getElementsByTagName('tr');
	for (i = 0; i < tr.length; i++) {
		tdOrderNumber = tr[i].getElementsByTagName('td')[0];
		tdCustomerNumber = tr[i].getElementsByTagName('td')[3];
		tdCustomer = tr[i].getElementsByTagName('td')[4];
		if (tdOrderNumber && tdCustomerNumber && tdCustomer) {
			textValueOrderNumber = tdOrderNumber.textContent || tdOrderNumber.innerText;
			textValueCustomerNumber = tdCustomerNumber.textContent || tdCustomerNumber.innerText;
			textValueCustomer = tdCustomer.textContent || tdCustomer.innerText;
			if (textValueOrderNumber.toUpperCase().indexOf(filter) > -1 || textValueCustomerNumber.toUpperCase().indexOf(filter) > -1 || textValueCustomer.toUpperCase().indexOf(filter) > -1) {
				tr[i].style.display = '';
			} else {
				tr[i].style.display = 'none';
			};
		};
	};
};

function filterByNotApproved() {
	var table, tr, i, btnNotApproved, orderStatus, spanStatus, textSpanStatus, filter;
	filter = localizedData.statusNotApproved.toUpperCase();
	btnNotApproved = document.getElementById('btnNotApproved');
	table = document.getElementById('ordersTable');
	tr = table.getElementsByTagName('tr');
	for (i = 0; i < tr.length; i++) {
		orderStatus = tr[i].getElementsByTagName('td')[10];
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

function filterByApproved() {
	var table, tr, i, btnNotApproved, orderStatus, spanStatus, textSpanStatus, filter;
	filter = localizedData.statusApproved.toUpperCase();
	btnNotApproved = document.getElementById('btnApproved');
	table = document.getElementById('ordersTable');
	tr = table.getElementsByTagName('tr');
	for (i = 0; i < tr.length; i++) {
		orderStatus = tr[i].getElementsByTagName('td')[10];
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

function filterByRejected() {
	var table, tr, i, btnNotApproved, orderStatus, spanStatus, textSpanStatus, filter;
	filter = localizedData.statusRejected.toUpperCase();
	btnNotApproved = document.getElementById('btnRejected');
	table = document.getElementById('ordersTable');
	tr = table.getElementsByTagName('tr');
	for (i = 0; i < tr.length; i++) {
		console.log(tr[i]);
		orderStatus = tr[i].getElementsByTagName('td')[10];
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

function filterClear() {
	var table, tr, i, btnClear;
	btnNotApproved = document.getElementById('btnClear');
	table = document.getElementById('ordersTable');
	tr = table.getElementsByTagName('tr');
	for (i = 0; i < tr.length; i++) {
		tr[i].style.display = '';
	};
};