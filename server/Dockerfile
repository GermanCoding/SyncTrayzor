FROM php:8.5-apache@sha256:609de4eac65a03f20975441c9c3f313811d785575f0d02413c630753ab5c5532
RUN a2enmod rewrite
RUN rm -f /var/www/html/index.html
COPY version-check.php /var/www/html/version-check.php
COPY .htaccess /var/www/html/.htaccess
RUN chown -R www-data:www-data /var/www/html/
EXPOSE 80