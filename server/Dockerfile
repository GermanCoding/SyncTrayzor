FROM php:8.5-apache@sha256:434b2fa9706d6d92832aadc1508324cf832e3dd33abf0844bafd1469d3e7ee14
RUN a2enmod rewrite
RUN rm -f /var/www/html/index.html
COPY version-check.php /var/www/html/version-check.php
COPY .htaccess /var/www/html/.htaccess
RUN chown -R www-data:www-data /var/www/html/
EXPOSE 80